using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.DemoStageControl;
using Game.Feature.DemoStageControl.UI;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class DemoStageControlPanelTests
    {
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
        public void DemoStageControlPanel_StartSelectedStage_CallsCommandPort()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = new DemoStageControlPanelRuntime(
                view,
                commandPort,
                commandPort,
                CreatePayload(commandPort),
                () => { });

            FindButton(view.transform, "Start Selected Stage").onClick.Invoke();

            Assert.That(commandPort.StartStageCalls, Is.EqualTo(1));
            Assert.That(commandPort.LastStartedStageId, Is.EqualTo(commandPort.StageId));
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
            Assert.That(source, Does.Not.Contain("StageCompletionCommitter"));
            Assert.That(source, Does.Not.Contain("SaveSlotStore"));
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
                () => { });

            Assert.That(CollectText(view.transform), Does.Contain("Player Invincible: ON"));
            Assert.That(FindButton(view.transform, "Player Invincible").interactable, Is.True);
        }

        [Test]
        public void DemoGameplayOverride_ViewDoesNotReferenceWorldState()
        {
            var source = File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelView.cs");

            Assert.That(source, Does.Not.Contain("WorldState"));
            Assert.That(source, Does.Not.Contain("EntityState"));
            Assert.That(source, Does.Not.Contain("TickPipeline"));
            Assert.That(source, Does.Not.Contain("SaveSlotStore"));
        }

        [Test]
        public void DemoStageControlPanel_DoesNotDependOnDebugCommandsPopup()
        {
            var sources = string.Join(
                Environment.NewLine,
                File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelModels.cs"),
                File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelRuntime.cs"),
                File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelView.cs"));

            Assert.That(sources, Does.Not.Contain("DebugCommands"));
            Assert.That(sources, Does.Not.Contain("DebugCommandAccess"));
        }

        private DemoStageControlPanelView CreateView()
        {
            _root = new GameObject(nameof(DemoStageControlPanelTests), typeof(RectTransform));
            return DemoStageControlPanelView.CreateRuntime(_root.transform);
        }

        private static DemoStageControlPanelPayload CreatePayload(RecordingCommandPort commandPort)
        {
            return new DemoStageControlPanelPayload(
                commandPort.GetStages(),
                commandPort.GetStatus(),
                commandPort.GetOverrideStatus());
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

        private sealed class RecordingCommandPort : IDemoStageControlCommandPort, IDemoGameplayOverrideCommandPort
        {
            public readonly StageId StageId = StageId.CreateOrThrow("stage-0-1");
            private string _overrideMessage = string.Empty;
            private string _message = string.Empty;

            public int ForceClearCalls { get; private set; }

            public StageId LastStartedStageId { get; private set; } = StageId.None;

            public int StartStageCalls { get; private set; }

            public bool PlayerInvincible { get; private set; }

            public int SetPlayerInvincibleCalls { get; private set; }

            public IReadOnlyList<DemoStageControlStageItem> GetStages()
            {
                return new[]
                {
                    new DemoStageControlStageItem(StageId, "Stage 0-1", true, true),
                };
            }

            public DemoStageControlStatus GetStatus()
            {
                return new DemoStageControlStatus(
                    StageId,
                    StageId,
                    isSceneTransitionInProgress: false,
                    isCompletionInProgress: false,
                    _message);
            }

            public DemoStageControlResult StartStage(StageId stageId)
            {
                StartStageCalls++;
                LastStartedStageId = stageId;
                _message = $"started {stageId.Value}";
                return DemoStageControlResult.Ok(_message);
            }

            public DemoStageControlResult ForceClearCurrentStage()
            {
                ForceClearCalls++;
                _message = "forced clear";
                return DemoStageControlResult.Ok(_message);
            }

            public DemoStageControlResult SetPlayerInvincible(bool enabled)
            {
                SetPlayerInvincibleCalls++;
                PlayerInvincible = enabled;
                _overrideMessage = enabled ? "Player Invincible ON" : "Player Invincible OFF";
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
        }
    }
}
