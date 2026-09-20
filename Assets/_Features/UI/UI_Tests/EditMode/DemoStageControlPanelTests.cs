using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.DemoStageControl;
using Game.Feature.DemoStageControl.UI;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
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
            Assert.That(CountMissingScripts(prefab), Is.Zero);
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
            CollectionAssert.AreEquivalent(texts, view.CreateTypographyTargets());
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
        public void DemoStageControlPanel_IncompleteTypographyTargets_FailFast()
        {
            var view = CreateView();
            var serialized = new SerializedObject(view);
            var targets = serialized.FindProperty("_typographyTargets");
            Assert.That(targets, Is.Not.Null);
            Assert.That(targets.arraySize, Is.GreaterThan(1));
            targets.arraySize--;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<InvalidOperationException>(view.ValidateAuthoredReferences);
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

        [TestCase(DemoAction.Previous, "Previous")]
        [TestCase(DemoAction.Next, "Next")]
        [TestCase(DemoAction.Start, "Start Selected Stage")]
        [TestCase(DemoAction.ForceClear, "Force Clear Current Stage")]
        [TestCase(DemoAction.Invincible, "Player Invincible")]
        [TestCase(DemoAction.Close, "Close")]
        public void DemoStageControlPanel_AllActions_PointerAndSubmitInvokeExactlyOnce(
            DemoAction action,
            string buttonName)
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = CreateRuntime(view, commandPort);
            var selectedChanges = 0;
            var closes = 0;
            view.SelectedStageChanged += _ => selectedChanges++;
            view.CompletionRequested += _ => closes++;

            FindButton(view.transform, buttonName).onClick.Invoke();
            FocusAction(view, action);
            Assert.That(view.HandleSubmit(), Is.True);

            switch (action)
            {
                case DemoAction.Previous:
                case DemoAction.Next:
                    Assert.That(selectedChanges, Is.EqualTo(2));
                    break;
                case DemoAction.Start:
                    Assert.That(commandPort.StartStageCalls, Is.EqualTo(2));
                    break;
                case DemoAction.ForceClear:
                    Assert.That(commandPort.ForceClearCalls, Is.EqualTo(2));
                    break;
                case DemoAction.Invincible:
                    Assert.That(commandPort.SetPlayerInvincibleCalls, Is.EqualTo(2));
                    break;
                case DemoAction.Close:
                    Assert.That(closes, Is.EqualTo(2));
                    break;
                default:
                    Assert.Fail($"Unhandled demo action {action}.");
                    break;
            }
        }

        [Test]
        public void DemoStageControlPanel_NavigationFollowsAuthoredGraphWithoutWrap()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = CreateRuntime(view, commandPort);

            view.OnNavigationFocusGained();
            Assert.That(view.HandleSubmit(), Is.True, "Initial focus must be Start.");
            Assert.That(commandPort.StartStageCalls, Is.EqualTo(1));

            view.OnNavigationFocusGained();
            Assert.That(view.HandleNavigate(UiNavigationCommand.Up), Is.True);
            Assert.That(view.HandleNavigate(UiNavigationCommand.Left), Is.False, "Previous must not wrap left.");
            Assert.That(view.HandleNavigate(UiNavigationCommand.Right), Is.True);
            Assert.That(view.HandleNavigate(UiNavigationCommand.Right), Is.False, "Next must not wrap right.");
            Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True, "Selectors must move down to Start.");
            Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.False, "Close must not wrap down.");
        }

        [Test]
        public void DemoStageControlPanel_DisabledStart_UpFromForceClearSkipsToSelector()
        {
            var commandPort = new RecordingCommandPort { IsSceneTransitionInProgress = true };
            var view = CreateView();
            using var runtime = CreateRuntime(view, commandPort);
            var selectedChanges = 0;
            view.SelectedStageChanged += _ => selectedChanges++;

            view.OnNavigationFocusGained();
            Assert.That(view.HandleNavigate(UiNavigationCommand.Up), Is.True);
            Assert.That(view.HandleSubmit(), Is.True);

            Assert.That(selectedChanges, Is.EqualTo(1));
            Assert.That(commandPort.ForceClearCalls, Is.Zero);
        }

        [Test]
        public void DemoStageControlPanel_NonTopmostBlocksPointerSubmitAndCancel()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = CreateRuntime(view, commandPort);
            var closes = 0;
            view.CompletionRequested += _ => closes++;

            view.SetIsTopmost(false);
            FindButton(view.transform, "Start Selected Stage").onClick.Invoke();

            Assert.That(view.HandleSubmit(), Is.False);
            Assert.That(view.HandleCancel(), Is.False);
            Assert.That(commandPort.StartStageCalls, Is.Zero);
            Assert.That(closes, Is.Zero);
        }

        [Test]
        public void DemoStageControlPanel_ReactivationDoesNotDuplicateButtonListeners()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = CreateRuntime(view, commandPort);

            view.gameObject.SetActive(false);
            view.gameObject.SetActive(true);
            FindButton(view.transform, "Start Selected Stage").onClick.Invoke();

            Assert.That(commandPort.StartStageCalls, Is.EqualTo(1));
        }

        [Test]
        public void GameplayPopupRuntimeFactory_DemoRequestInstantiatesRepositoryCatalogPrefab()
        {
            var commandPort = new RecordingCommandPort();
            var layer = CreatePopupLayer();
            var factory = new GameplayPopupRuntimeFactory(
                layer,
                UiTestPrefabAssetUtility.LoadPopupCatalog(),
                commandPort,
                commandPort,
                new StageNameResolver());

            var result = factory.Create(new PopupRequest(
                PopupId.DemoStageControl,
                CreatePayload(commandPort)));
            try
            {
                Assert.That(result.Runtime, Is.TypeOf<DemoStageControlPanelRuntime>());
                var view = layer.FindPopupView<DemoStageControlPanelView>();
                Assert.That(view, Is.Not.Null);
                Assert.That(view.transform.parent, Is.EqualTo(layer.ContentRoot));
            }
            finally
            {
                result.Runtime.Dispose();
            }
        }

        [Test]
        public void GameplayPopupRuntimeFactory_MissingDemoCatalogPrefabFailsFast()
        {
            var commandPort = new RecordingCommandPort();
            var layer = CreatePopupLayer();
            var catalog = ScriptableObject.CreateInstance<PopupPrefabCatalog>();
            try
            {
                var factory = new GameplayPopupRuntimeFactory(
                    layer,
                    catalog,
                    commandPort,
                    commandPort,
                    new StageNameResolver());

                var exception = Assert.Throws<InvalidOperationException>(() => factory.Create(
                    new PopupRequest(PopupId.DemoStageControl, CreatePayload(commandPort))));
                Assert.That(exception.Message, Does.Contain("missing a canonical prefab"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
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

        private PopupLayerView CreatePopupLayer()
        {
            _root = new GameObject(nameof(DemoStageControlPanelTests), typeof(RectTransform));
            var layerRoot = new GameObject("PopupLayerRoot", typeof(RectTransform));
            layerRoot.transform.SetParent(_root.transform, false);
            var popupRoot = new GameObject("PopupRoot", typeof(RectTransform));
            popupRoot.transform.SetParent(layerRoot.transform, false);
            var backdrop = new GameObject(
                "Backdrop",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(Button));
            backdrop.transform.SetParent(popupRoot.transform, false);
            var content = new GameObject("PopupContentRoot", typeof(RectTransform));
            content.transform.SetParent(popupRoot.transform, false);
            var layer = layerRoot.AddComponent<PopupLayerView>();
            layer.Configure(
                popupRoot,
                backdrop.GetComponent<CanvasGroup>(),
                backdrop.GetComponent<Image>(),
                backdrop.GetComponent<Button>(),
                content.GetComponent<RectTransform>());
            return layer;
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
            Button button = null;
            foreach (var candidate in root.GetComponentsInChildren<Button>(true))
            {
                if (candidate.name == name)
                {
                    button = candidate;
                    break;
                }
            }

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

        private static int CountMissingScripts(GameObject root)
        {
            var count = 0;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
            return count;
        }

        private static void FocusAction(DemoStageControlPanelView view, DemoAction action)
        {
            view.OnNavigationFocusGained();
            switch (action)
            {
                case DemoAction.Previous:
                    Assert.That(view.HandleNavigate(UiNavigationCommand.Up), Is.True);
                    break;
                case DemoAction.Next:
                    Assert.That(view.HandleNavigate(UiNavigationCommand.Up), Is.True);
                    break;
                case DemoAction.Start:
                    break;
                case DemoAction.ForceClear:
                    Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
                    break;
                case DemoAction.Invincible:
                    Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
                    Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
                    break;
                case DemoAction.Close:
                    Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
                    Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
                    Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        public enum DemoAction
        {
            Previous,
            Next,
            Start,
            ForceClear,
            Invincible,
            Close,
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
            public readonly StageId SecondStageId = StageId.CreateOrThrow("stage-0-2");
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
                    new DemoStageControlStageItem(SecondStageId, string.Empty, false, true),
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
