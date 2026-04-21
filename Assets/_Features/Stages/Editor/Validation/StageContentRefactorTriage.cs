using System;

namespace Game.Feature.Stages.Editor.Validation
{
    public enum StageContentRefactorLane
    {
        SunsetCore = 0,
        StageIdUxContract = 1,
        AdjacentBroadBacklog = 2,
    }

    public static class StageContentRefactorTriage
    {
        private static readonly string[] SunsetCoreTokens =
        {
            "compat",
            "legacy stagedefinition",
            "serializedstagecontententry",
            "resolvelegacy",
            "presentationid",
            "stagecatalogresolver",
            "stagecontententry",
            "stageloadrequest",
            "stageruntimecontentresolver",
            "stagecatalogvalidator",
            "alias",
            "grandfather",
            "canonical load path",
            "ci blocker",
            "validator",
        };

        private static readonly string[] StageIdUxTokens =
        {
            "stageid payload",
            "stageid launch",
            "stagelaunchrouter",
            "istagelaunchrouter",
            "stagenavigationrequest",
            "screenaction.launchstage",
            "screenactionkind.launchstage",
            "stageresultscreenpayload",
            "uiflowcoordinator",
            "direct gameplay screen",
            "screenid.gameplay",
            "continue/retry/next-stage",
        };

        private static readonly string[] BroadBacklogTokens =
        {
            "presenter",
            "view",
            "prefab",
            "layout",
            "backstack",
            "audio",
            "host glue",
            "tooltip",
            "screen shell",
        };

        public static StageContentRefactorLane Classify(string oracle, string source = null, string detail = null)
        {
            var text = string.Concat(
                oracle ?? string.Empty,
                "\n",
                source ?? string.Empty,
                "\n",
                detail ?? string.Empty);

            if (ContainsAny(text, StageIdUxTokens))
            {
                return StageContentRefactorLane.StageIdUxContract;
            }

            if (ContainsAny(text, SunsetCoreTokens))
            {
                return StageContentRefactorLane.SunsetCore;
            }

            if (ContainsAny(text, BroadBacklogTokens))
            {
                return StageContentRefactorLane.AdjacentBroadBacklog;
            }

            return StageContentRefactorLane.AdjacentBroadBacklog;
        }

        private static bool ContainsAny(string text, string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(text) || tokens == null)
            {
                return false;
            }

            for (var i = 0; i < tokens.Length; i++)
            {
                if (text.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
