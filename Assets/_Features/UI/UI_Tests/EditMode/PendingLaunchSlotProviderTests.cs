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
        private string _activeKey;

        [SetUp]
        public void SetUp()
        {
            _saveKey = CreatePrefsKey("saves");
            _activeKey = CreatePrefsKey("active");
            PlayerPrefs.DeleteKey(_saveKey);
            PlayerPrefs.DeleteKey(_activeKey);
            CampaignChanceHudDiagnostics.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_saveKey);
            PlayerPrefs.DeleteKey(_activeKey);
            PlayerPrefs.Save();
            CampaignChanceHudDiagnostics.IsEnabled = false;
            CampaignChanceHudDiagnostics.Clear();
        }

        [Test]
        public void MainMenu_NewGame_SetsPendingLaunchSlotThroughProvider()
        {
            var saveStore = new SaveSlotStore(_saveKey);
            var pendingProvider = new SpyPendingLaunchSlotProvider();
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var resolver = CreateResolver();
            var controller = CreateController(saveStore, pendingProvider, resolver, router, confirmPort);
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 2,
                CurrentStageId = StageId.CreateOrThrow("stage-2-1"),
                CurrentLevelGroupId = "level-2",
            });

            controller.HandleIntent(new SaveSlotIntent(2, SaveSlotIntentKind.NewGame));
            confirmPort.Complete(true);

            Assert.That(pendingProvider.SetSlots, Is.EqualTo(new[] { 2 }));
            Assert.That(saveStore.LoadSlot(2).CurrentStageId, Is.EqualTo(resolver.FirstStageId));
            Assert.That(router.Requests.Count, Is.EqualTo(1));
            Assert.That(router.Requests[0].StageId, Is.EqualTo(resolver.FirstStageId));
        }

        [Test]
        public void MainMenu_Continue_SetsPendingLaunchSlotFromExplicitSelectedSlot()
        {
            var saveStore = new SaveSlotStore(_saveKey);
            var pendingProvider = new SpyPendingLaunchSlotProvider();
            var router = new FakeStageLaunchRouter();
            var selectedStage = StageId.CreateOrThrow("stage-3-1");
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 3,
                CurrentStageId = selectedStage,
                CurrentLevelGroupId = "level-3",
            });
            var controller = CreateController(saveStore, pendingProvider, CreateResolver(), router);

            controller.Continue(3);

            Assert.That(pendingProvider.SetSlots, Is.EqualTo(new[] { 3 }));
            Assert.That(router.Requests.Count, Is.EqualTo(1));
            Assert.That(router.Requests[0].StageId, Is.EqualTo(selectedStage));
        }

        [Test]
        public void MainMenu_Continue_ExplicitIntentOverridesPreviousPendingLaunchSlot()
        {
            var saveStore = new SaveSlotStore(_saveKey);
            var pendingProvider = new SpyPendingLaunchSlotProvider();
            var router = new FakeStageLaunchRouter();
            var selectedStage = StageId.CreateOrThrow("stage-1-2");
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = selectedStage,
                CurrentLevelGroupId = "level-1",
            });
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 3,
                CurrentStageId = StageId.CreateOrThrow("stage-3-1"),
                CurrentLevelGroupId = "level-3",
            });
            pendingProvider.SetPendingLaunchSlot(3);
            var controller = CreateController(saveStore, pendingProvider, CreateResolver(), router);

            controller.HandleIntent(new SaveSlotIntent(1, SaveSlotIntentKind.Continue));

            Assert.That(pendingProvider.SetSlots, Is.EqualTo(new[] { 3, 1 }));
            Assert.That(pendingProvider.TryGetPendingLaunchSlot(out var pendingSlot), Is.True);
            Assert.That(pendingSlot, Is.EqualTo(1));
            Assert.That(router.Requests.Count, Is.EqualTo(1));
            Assert.That(router.Requests[0].StageId, Is.EqualTo(selectedStage));
        }

        [Test]
        public void MainMenu_Continue_DoesNotUseLastPlayedSlotNumber()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");

            Assert.That(source, Does.Contain("SetPendingLaunchSlot(slotNumber)"));
            Assert.That(source, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(source, Does.Not.Contain("CampaignProfileDocument"));
        }

        [Test]
        public void MainMenu_Delete_ClearsPendingLaunchSlotWhenDeletedSlotMatches()
        {
            var saveStore = new SaveSlotStore(_saveKey);
            var pendingProvider = new SpyPendingLaunchSlotProvider();
            pendingProvider.SetPendingLaunchSlot(1);
            saveStore.SaveSlot(CreateExistingSlot(1));
            var confirmPort = new FakeConfirmPopupPort();
            var controller = CreateController(saveStore, pendingProvider, CreateResolver(), confirmPort: confirmPort);

            controller.RequestDelete(1);
            confirmPort.Complete(true);

            Assert.That(saveStore.LoadSlot(1).IsEmpty, Is.True);
            Assert.That(pendingProvider.ClearCallCount, Is.EqualTo(1));
            Assert.That(pendingProvider.TryGetPendingLaunchSlot(out _), Is.False);
        }

        [Test]
        public void MainMenu_Delete_DoesNotClearPendingLaunchSlotWhenDeletedSlotDiffers()
        {
            var saveStore = new SaveSlotStore(_saveKey);
            var pendingProvider = new SpyPendingLaunchSlotProvider();
            pendingProvider.SetPendingLaunchSlot(2);
            saveStore.SaveSlot(CreateExistingSlot(1));
            saveStore.SaveSlot(CreateExistingSlot(2));
            var confirmPort = new FakeConfirmPopupPort();
            var controller = CreateController(saveStore, pendingProvider, CreateResolver(), confirmPort: confirmPort);

            controller.RequestDelete(1);
            confirmPort.Complete(true);

            Assert.That(saveStore.LoadSlot(1).IsEmpty, Is.True);
            Assert.That(pendingProvider.ClearCallCount, Is.Zero);
            Assert.That(pendingProvider.TryGetPendingLaunchSlot(out var pendingSlot), Is.True);
            Assert.That(pendingSlot, Is.EqualTo(2));
        }

        [Test]
        public void MainMenu_LoadSlotList_StillUsesSaveSlotStore()
        {
            var saveStore = new SaveSlotStore(_saveKey);
            saveStore.SaveSlot(CreateExistingSlot(1));
            var controller = CreateController(saveStore, new SpyPendingLaunchSlotProvider(), CreateResolver());

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards[0].State, Is.EqualTo(SaveSlotCardState.Existing));
            Assert.That(ReadRepoFile("Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs"),
                Does.Contain("_saveSlotStore.LoadAllWithReport()"));
            Assert.That(ReadRepoFile("Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs"),
                Does.Not.Contain("CampaignSaveService"));
        }

        [Test]
        public void MainMenuUiFlowInstaller_SharesOnePendingProviderAdapterBetweenControllerAndCinematicRouter()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");

            Assert.That(
                CountOccurrences(source, "new ActiveSlotProviderPendingLaunchAdapter(activeSlotProvider)"),
                Is.EqualTo(1));
            Assert.That(source, Does.Contain(
                "var pendingLaunchSlotProvider = new ActiveSlotProviderPendingLaunchAdapter(activeSlotProvider);"));
            Assert.That(source, Does.Contain("pendingLaunchSlotProvider,\n                EnsureCinematicFlowCoordinator()"));
            Assert.That(source, Does.Contain(
                "pendingLaunchSlotProvider,\n                sequenceResolver,\n                stageLaunchRouter"));
            Assert.That(source, Does.Contain("pendingLaunchSlotProvider.DiagnosticsKey"));
        }

        private MainMenuController CreateController(
            SaveSlotStore saveStore,
            IPendingLaunchSlotProvider pendingProvider,
            CampaignStageSequenceResolver resolver,
            IStageLaunchRouter router = null,
            FakeConfirmPopupPort confirmPort = null)
        {
            return new MainMenuController(
                saveStore,
                pendingProvider,
                resolver,
                router ?? new FakeStageLaunchRouter(),
                confirmPort ?? new FakeConfirmPopupPort(),
                pendingLaunchSlotProviderDiagnosticsKey: _activeKey);
        }

        private static CampaignStageSequenceResolver CreateResolver()
        {
            return new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());
        }

        private static SaveSlotData CreateExistingSlot(int slotNumber)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
            };
        }

        private static string CreatePrefsKey(string suffix)
        {
            return "Game.Feature.UI.Tests.PendingLaunchSlotProviderTests." + suffix + "." + Guid.NewGuid().ToString("N");
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

        private sealed class SpyPendingLaunchSlotProvider : IPendingLaunchSlotProvider
        {
            private int _pendingSlotNumber;

            public int ClearCallCount { get; private set; }

            public List<int> SetSlots { get; } = new();

            public bool TryGetPendingLaunchSlot(out int slotNumber)
            {
                if (SaveSlotStore.IsValidSlotNumber(_pendingSlotNumber))
                {
                    slotNumber = _pendingSlotNumber;
                    return true;
                }

                slotNumber = 0;
                return false;
            }

            public void SetPendingLaunchSlot(int slotNumber)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
                _pendingSlotNumber = slotNumber;
                SetSlots.Add(slotNumber);
            }

            public void ClearPendingLaunchSlot()
            {
                _pendingSlotNumber = 0;
                ClearCallCount++;
            }

            public bool IsPendingLaunchSlot(int slotNumber)
            {
                return SaveSlotStore.IsValidSlotNumber(slotNumber) && _pendingSlotNumber == slotNumber;
            }
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
    }
}
