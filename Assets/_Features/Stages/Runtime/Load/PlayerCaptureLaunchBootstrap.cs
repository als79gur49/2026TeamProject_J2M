using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    public static class PlayerCaptureLaunchBootstrap
    {
        public const string CampaignTempSlotArgument = "--capture-campaign-temp-slot";
        public const string CampaignNormalSlotArgument = "--capture-campaign-normal-slot";
        public const string CampaignTempSlotChancesArgument =
            "--capture-campaign-temp-slot-chances";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrimeLaunchContextBeforeSceneLoad()
        {
            TryPrimeFromArguments(Environment.GetCommandLineArgs(), logErrors: true, out _);
        }

        public static bool TryPrimeFromArguments(
            string[] args,
            bool logErrors,
            out string error)
        {
            if (!PlayerCaptureLaunchOptions.TryParse(args, out var options, out error))
            {
                if (logErrors)
                {
                    Debug.LogError(error);
                }

                return false;
            }

            if (!options.HasCaptureStage)
            {
                return true;
            }

            var normalCampaignCapture = Array.Exists(
                args,
                argument => string.Equals(
                    argument,
                    CampaignNormalSlotArgument,
                    StringComparison.Ordinal));
            var tempCampaignCapture = Array.Exists(
                args,
                argument => string.Equals(
                    argument,
                    CampaignTempSlotArgument,
                    StringComparison.Ordinal));
            if (normalCampaignCapture && tempCampaignCapture)
            {
                error = "Player capture cannot request normal and DirectPlay Campaign slots together.";
                if (logErrors)
                {
                    Debug.LogError(error);
                }

                return false;
            }

            if (normalCampaignCapture)
            {
                EditorDirectPlayContextStore.Clear();
                var saveStore = CampaignSaveCompositionProvider.CreateProductionProfileBacked();
                var activeSlot = CampaignSaveCompositionProvider.CreateProductionActiveSlotProvider(
                    saveStore);
                saveStore.ClearAll();
                activeSlot.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = options.StageId,
                    CurrentLevelGroupId = "player-capture-bootstrap",
                    RemainingChances = SaveSlotStore.DefaultRemainingChances,
                    LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
                });
                activeSlot.SetActiveSlot(1);
                var request = new StageNavigationRequest(
                    options.StageId,
                    StageNavigationKind.Retry,
                    "pause-retry");
                if (!StageLaunchContextStore.TrySetCurrent(
                        StageLaunchContext.CreatePendinglessReload(request)))
                {
                    error = "Player capture could not register the normal Campaign launch context.";
                    if (logErrors)
                    {
                        Debug.LogError(error);
                    }

                    return false;
                }

                Debug.Log(
                    $"Player capture normal Campaign slot launch context primed with StageId " +
                    $"'{options.StageId.Value}'.");
                return true;
            }

            StageLaunchContextStore.SetCurrent(options.StageId);

            if (tempCampaignCapture)
            {
                var remainingChances = 2;
                for (var i = 0; i < args.Length - 1; i++)
                {
                    if (!string.Equals(
                            args[i],
                            CampaignTempSlotChancesArgument,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!int.TryParse(args[i + 1], out remainingChances) ||
                        remainingChances < 1 ||
                        remainingChances > SaveSlotStore.DefaultRemainingChances)
                    {
                        error =
                            $"{CampaignTempSlotChancesArgument} must be between 1 and " +
                            $"{SaveSlotStore.DefaultRemainingChances}.";
                        if (logErrors)
                        {
                            Debug.LogError(error);
                        }

                        return false;
                    }

                    break;
                }

                var saveStore = new SaveSlotStore(
                    EditorDirectPlayContextStore.TempSaveSlotStoreKey,
                    EditorDirectPlayContextStore.TempActiveSlotProviderKey);
                var activeSlot = new ActiveSlotProvider(
                    EditorDirectPlayContextStore.TempActiveSlotProviderKey);
                saveStore.ClearAll();
                activeSlot.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = options.StageId,
                    CurrentLevelGroupId = "level-01",
                    RemainingChances = remainingChances,
                    LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
                });
                activeSlot.SetActiveSlot(1);
                EditorDirectPlayContextStore.SetCurrent(
                    EditorDirectPlayContext.CreateCampaignTempSlot(
                        options.StageId,
                        remainingChances));
                Debug.Log(
                    $"Player capture campaign temp-slot launch context primed with StageId " +
                    $"'{options.StageId.Value}' and remainingChances={remainingChances}.");
                return true;
            }

            EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(options.StageId));
            Debug.Log($"Player capture launch context primed with StageId '{options.StageId.Value}'.");
            return true;
        }
    }
}
