using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    public static class PlayerCaptureLaunchBootstrap
    {
        public const string CampaignTempSlotArgument = "--capture-campaign-temp-slot";
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

            StageLaunchContextStore.SetCurrent(options.StageId);
            if (Array.Exists(
                    args,
                    argument => string.Equals(
                        argument,
                        CampaignTempSlotArgument,
                        StringComparison.Ordinal)))
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
