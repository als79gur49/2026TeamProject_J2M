using System;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    internal static class CampaignStageSceneTestLaunch
    {
        internal static void Prime(StageId stageId)
        {
            PrimeTemporarySlot(stageId);
            StageLaunchContextStore.SetCurrent(stageId);
        }

        internal static void PrimeTemporarySlot(StageId stageId)
        {
            var content = Resources.Load<PlayerCaptureCampaignContent>(
                "PlayerCaptureCampaignContent");
            Assert.That(content, Is.Not.Null);
            Assert.That(content.TryGetLevelGroupId(stageId, out var levelGroupId, out var error),
                Is.True, error);

            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
            var saveStore = CampaignSaveCompositionProvider.CreateTemporaryProfileBacked();
            var activeSlot = CampaignSaveCompositionProvider.CreateTemporaryActiveSlotProvider(
                saveStore);
            saveStore.ClearAll();
            activeSlot.ClearActiveSlot();
            saveStore.ImportSlotSeed(new CampaignSlotSeedImportRequest(
                1, stageId, levelGroupId, CampaignSaveSlotPolicy.DefaultRemainingChances,
                DateTimeOffset.UtcNow.ToString("O")));
            activeSlot.SetActiveSlot(1);
            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateCampaignTempSlot(
                    stageId, CampaignSaveSlotPolicy.DefaultRemainingChances));
        }
    }
}
