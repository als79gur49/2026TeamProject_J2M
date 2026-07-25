using System;

namespace Game.Feature.Stages
{
    public static class StageDisplayNameKeys
    {
        public const string Table = "Stage";
        public const string Suffix = ".display_name";

        public static string ForStage(StageId stageId)
        {
            return stageId.IsValid ? ForStageIdValue(stageId.Value) : string.Empty;
        }

        public static string ForStageIdValue(string stageIdValue)
        {
            return string.IsNullOrWhiteSpace(stageIdValue)
                ? string.Empty
                : $"stage.{stageIdValue.Trim()}{Suffix}";
        }

        public static string RequireForStage(StageId stageId, string key)
        {
            var normalized = Normalize(key);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            if (!stageId.IsValid)
            {
                return string.Empty;
            }

            throw new InvalidOperationException(
                $"Stage '{stageId.Value}' must define a canonical displayNameKey.");
        }

        public static bool IsKeyForStage(StageId stageId, string key)
        {
            return string.Equals(ForStage(stageId), Normalize(key), StringComparison.Ordinal);
        }

        public static string Normalize(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
        }
    }
}
