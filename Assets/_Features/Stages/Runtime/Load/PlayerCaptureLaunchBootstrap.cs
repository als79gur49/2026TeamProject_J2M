using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    public static class PlayerCaptureLaunchBootstrap
    {
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
            EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(options.StageId));
            Debug.Log($"Player capture launch context primed with StageId '{options.StageId.Value}'.");
            return true;
        }
    }
}
