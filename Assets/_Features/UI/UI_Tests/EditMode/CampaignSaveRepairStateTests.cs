using System;
using System.Linq;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class CampaignSaveRepairStateTests
    {
        [Test]
        public void MainMenuRepairRequiredLoadDoesNotRenderFreshEmptySlots()
        {
            var controller = new MainMenuController(
                new RepairRequiredSaveSlotStore(),
                new NoOpPendingLaunchSlotProvider(),
                new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance()),
                new NoOpStageLaunchRouter(),
                new NoOpConfirmPopupPort());

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards, Has.Count.EqualTo(SaveSlotStore.SlotCount));
            Assert.That(viewModel.SlotCards.All(card => card.State == SaveSlotCardState.Empty), Is.False);
            Assert.That(viewModel.SlotCards.All(card => card.State == SaveSlotCardState.Corrupted), Is.True);
            Assert.That(viewModel.SlotCards.All(card => card.PrimaryIntentKind == SaveSlotIntentKind.None), Is.True);
        }

        private sealed class RepairRequiredSaveSlotStore : ICampaignSaveSlotStore
        {
            private readonly CampaignSaveLoadReport _report = new CampaignSaveLoadReport(
                CampaignSaveLoadStatus.CorruptRepairRequired,
                "profile.json was corrupt and no fallback was available.",
                "CampaignProfileDocument");

            public string DiagnosticsKey => "repair-required-test";

            public CampaignSaveLoadReport LastCampaignLoadReport => _report;

            public SaveSlotData[] LoadAll()
            {
                return LoadAllWithReport().Slots;
            }

            public CampaignSaveLoadResult LoadAllWithReport()
            {
                var slots = new SaveSlotData[SaveSlotStore.SlotCount];
                for (var i = 0; i < slots.Length; i++)
                {
                    slots[i] = SaveSlotData.CreateEmpty(i + 1);
                }

                return new CampaignSaveLoadResult(slots, _report);
            }

            public SaveSlotData LoadSlot(int slotNumber)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
                return SaveSlotData.CreateEmpty(slotNumber);
            }

            public void SaveSlot(SaveSlotData slot)
            {
                throw new InvalidOperationException("Repair-required backend must not be written in this test.");
            }

            public SaveSlotData InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt)
            {
                throw new InvalidOperationException("Repair-required backend must not initialize a new game in this test.");
            }

            public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation)
            {
                throw new InvalidOperationException("Repair-required backend must not be written in this test.");
            }

            public void DeleteSlot(int slotNumber)
            {
                throw new InvalidOperationException("Repair-required backend must not be written in this test.");
            }

            public void ClearAll()
            {
                throw new InvalidOperationException("Repair-required backend must not be written in this test.");
            }
        }

        private sealed class NoOpPendingLaunchSlotProvider : IPendingLaunchSlotProvider
        {
            public bool TryGetPendingLaunchSlot(out int slotNumber)
            {
                slotNumber = 0;
                return false;
            }

            public void SetPendingLaunchSlot(int slotNumber)
            {
            }

            public void ClearPendingLaunchSlot()
            {
            }

            public bool IsPendingLaunchSlot(int slotNumber)
            {
                return false;
            }
        }

        private sealed class NoOpStageLaunchRouter : IStageLaunchRouter
        {
            public void Launch(StageNavigationRequest request)
            {
            }
        }

        private sealed class NoOpConfirmPopupPort : IConfirmPopupPort
        {
            public void Request(ConfirmPopupPayload payload, Action<bool> completion)
            {
                completion?.Invoke(false);
            }
        }
    }
}
