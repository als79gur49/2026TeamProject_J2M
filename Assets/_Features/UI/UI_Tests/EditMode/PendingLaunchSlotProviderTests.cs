using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class PendingLaunchSlotProviderTests
    {
        private string _saveKey;

        [SetUp]
        public void SetUp()
        {
            _saveKey = CreatePrefsKey("saves");
            PlayerPrefs.DeleteKey(_saveKey);
            CampaignChanceHudDiagnostics.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_saveKey);
            PlayerPrefs.Save();
            CampaignChanceHudDiagnostics.IsEnabled = false;
            CampaignChanceHudDiagnostics.Clear();
        }

        [Test]
        public void MainMenu_NewGame_CreatesHandoffBeforeRouting()
        {
            var saveStore = new SaveSlotStore(_saveKey);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter(() =>
                Assert.That(handoffStore.TryPeek(out _), Is.True));
            var resolver = CreateResolver();
            var controller = CreateController(
                saveStore,
                handoffStore,
                resolver,
                router,
                confirmPort);
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 2,
                CurrentStageId = StageId.CreateOrThrow("stage-2-1"),
                CurrentLevelGroupId = "level-2",
            });

            controller.HandleIntent(new SaveSlotIntent(2, SaveSlotIntentKind.NewGame));
            confirmPort.Complete(true);

            Assert.That(handoffStore.TryPeek(out var handoff), Is.True);
            Assert.That(handoff.SlotNumber, Is.EqualTo(2));
            Assert.That(handoff.StageId, Is.EqualTo(resolver.FirstStageId));
            Assert.That(handoff.Source, Is.EqualTo("main-menu-new-game"));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void MainMenu_Continue_UsesExplicitSelectedSlot()
        {
            var saveStore = new SaveSlotStore(_saveKey);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var router = new FakeStageLaunchRouter();
            var selectedStage = StageId.CreateOrThrow("stage-3-1");
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 3,
                CurrentStageId = selectedStage,
                CurrentLevelGroupId = "level-3",
            });
            var controller = CreateController(
                saveStore,
                handoffStore,
                CreateResolver(),
                router);

            controller.Continue(3);

            Assert.That(handoffStore.TryPeek(out var handoff), Is.True);
            Assert.That(handoff.SlotNumber, Is.EqualTo(3));
            Assert.That(handoff.StageId, Is.EqualTo(selectedStage));
            Assert.That(handoff.Source, Is.EqualTo("main-menu-continue"));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void MainMenu_SecondRequest_CannotOverwriteAcceptedHandoff()
        {
            var saveStore = new SaveSlotStore(_saveKey);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var router = new FakeStageLaunchRouter();
            saveStore.SaveSlot(CreateExistingSlot(1, "stage-1-1"));
            saveStore.SaveSlot(CreateExistingSlot(3, "stage-3-1"));
            var controller = CreateController(
                saveStore,
                handoffStore,
                CreateResolver(),
                router);

            controller.Continue(3);
            Assert.That(handoffStore.TryPeek(out var first), Is.True);
            controller.Continue(1);

            Assert.That(handoffStore.TryPeek(out var stillPending), Is.True);
            Assert.That(stillPending, Is.SameAs(first));
            Assert.That(stillPending.SlotNumber, Is.EqualTo(3));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void MainMenu_Delete_ClearsOnlyMatchingPendingHandoff()
        {
            var saveStore = new SaveSlotStore(_saveKey);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            handoffStore.TryBegin(
                1,
                StageId.CreateOrThrow("stage-1-1"),
                StageNavigationKind.Continue,
                "delete-test",
                out _);
            saveStore.SaveSlot(CreateExistingSlot(1, "stage-1-1"));
            var confirmPort = new FakeConfirmPopupPort();
            var controller = CreateController(
                saveStore,
                handoffStore,
                CreateResolver(),
                confirmPort: confirmPort);

            controller.RequestDelete(1);
            confirmPort.Complete(true);

            Assert.That(saveStore.LoadSlot(1).IsEmpty, Is.True);
            Assert.That(handoffStore.TryPeek(out _), Is.False);
            Assert.That(handoffStore.ClearCount, Is.EqualTo(1));
        }

        [Test]
        public void MainMenu_Continue_DoesNotUseLastPlayedSlotNumber()
        {
            var source = ReadRepoFile(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");

            Assert.That(source, Does.Contain("_launchHandoffStore.TryBegin"));
            Assert.That(source, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(source, Does.Not.Contain("CampaignProfileDocument"));
        }

        [Test]
        public void MainMenuComposition_SharesApplicationSessionOwner()
        {
            var source = ReadRepoFile(
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");

            Assert.That(
                CountOccurrences(source, "CampaignLaunchHandoffSessionStore.Instance"),
                Is.EqualTo(1));
            Assert.That(source, Does.Contain("launchHandoffStore,\n                EnsureCinematicFlowCoordinator()"));
            Assert.That(source, Does.Contain("saveSlotStore,\n                launchHandoffStore,"));
            Assert.That(source, Does.Not.Contain("ActiveSlotProviderPendingLaunchAdapter"));
        }

        private static MainMenuController CreateController(
            SaveSlotStore saveStore,
            ICampaignLaunchHandoffStore handoffStore,
            CampaignStageSequenceResolver resolver,
            IStageLaunchRouter router = null,
            FakeConfirmPopupPort confirmPort = null)
        {
            return new MainMenuController(
                saveStore,
                handoffStore,
                resolver,
                router ?? new FakeStageLaunchRouter(),
                confirmPort ?? new FakeConfirmPopupPort());
        }

        private static CampaignStageSequenceResolver CreateResolver()
        {
            return new CampaignStageSequenceResolver(
                CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());
        }

        private static SaveSlotData CreateExistingSlot(int slotNumber, string stageId)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow(stageId),
                CurrentLevelGroupId = "level-" + slotNumber,
            };
        }

        private static string CreatePrefsKey(string suffix)
        {
            return
                "Game.Feature.UI.Tests.PendingLaunchSlotProviderTests." +
                suffix +
                "." +
                Guid.NewGuid().ToString("N");
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private sealed class FakeConfirmPopupPort : IConfirmPopupPort
        {
            private Action<bool> _completion;

            public void Request(ConfirmPopupPayload payload, Action<bool> completion)
            {
                _completion = completion ?? throw new ArgumentNullException(nameof(completion));
            }

            public void Complete(bool confirmed)
            {
                var completion = _completion;
                _completion = null;
                completion?.Invoke(confirmed);
            }
        }

        private sealed class FakeStageLaunchRouter : IStageLaunchRouter
        {
            private readonly Action _beforeRecord;

            public FakeStageLaunchRouter(Action beforeRecord = null)
            {
                _beforeRecord = beforeRecord;
            }

            public List<StageNavigationRequest> Requests { get; } = new();

            public void Launch(StageNavigationRequest request)
            {
                _beforeRecord?.Invoke();
                Requests.Add(request);
            }
        }
    }

    internal sealed class RecordingCampaignLaunchHandoffStore : ICampaignLaunchHandoffStore
    {
        private CampaignLaunchHandoff _pending;

        public int BeginCount { get; private set; }

        public int ClearCount { get; private set; }

        public int ConsumeCount { get; private set; }

        public bool TryBegin(
            int slotNumber,
            StageId stageId,
            StageNavigationKind navigationKind,
            string source,
            out CampaignLaunchHandoff handoff)
        {
            BeginCount++;
            if (_pending != null)
            {
                handoff = _pending;
                return false;
            }

            _pending = new CampaignLaunchHandoff(
                slotNumber,
                stageId,
                navigationKind,
                source,
                Guid.NewGuid());
            handoff = _pending;
            return true;
        }

        public bool TryPeek(out CampaignLaunchHandoff handoff)
        {
            handoff = _pending;
            return handoff != null;
        }

        public bool TryClear(Guid token)
        {
            if (_pending == null || _pending.Token != token)
            {
                return false;
            }

            ClearCount++;
            _pending = null;
            return true;
        }

        public bool TryConsume(Guid token, out CampaignLaunchHandoff handoff)
        {
            if (_pending == null || _pending.Token != token)
            {
                handoff = null;
                return false;
            }

            ConsumeCount++;
            handoff = _pending;
            _pending = null;
            return true;
        }
    }
}
