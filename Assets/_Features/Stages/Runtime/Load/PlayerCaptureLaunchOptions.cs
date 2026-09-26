using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public readonly struct PlayerCaptureLaunchOptions
    {
        public const string CaptureStageArg = "--capture-stage";
        public const string CaptureStageEditorArg = "-captureStage";

        private PlayerCaptureLaunchOptions(StageId stageId)
        {
            StageId = stageId;
            HasCaptureStage = stageId.IsValid;
        }

        public StageId StageId { get; }

        public bool HasCaptureStage { get; }

        public static bool TryParse(
            IReadOnlyList<string> args,
            out PlayerCaptureLaunchOptions options,
            out string error)
        {
            options = default;
            error = string.Empty;

            if (!TryReadArgumentValue(
                    args, CaptureStageArg, CaptureStageEditorArg,
                    out var rawStageId, out var stageArgWasPresent, out error))
            {
                return false;
            }

            if (!stageArgWasPresent)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(rawStageId))
            {
                error = $"{CaptureStageArg} requires a canonical StageId value.";
                return false;
            }

            if (!StageId.TryCreate(rawStageId, out var stageId))
            {
                error = $"'{rawStageId}' cannot be normalized into a canonical StageId for player capture launch.";
                return false;
            }

            options = new PlayerCaptureLaunchOptions(stageId);
            return true;
        }

        private static bool TryReadArgumentValue(
            IReadOnlyList<string> args,
            string longName,
            string editorName,
            out string value,
            out bool wasPresent,
            out string error)
        {
            value = string.Empty;
            wasPresent = false;
            error = string.Empty;

            if (args == null)
            {
                return true;
            }

            for (var i = 0; i < args.Count; i++)
            {
                var arg = args[i] ?? string.Empty;
                var isInline = TryReadInlineArgumentValue(arg, longName, out var inlineValue) ||
                               TryReadInlineArgumentValue(arg, editorName, out inlineValue);
                if (isInline)
                {
                    if (wasPresent)
                    {
                        error = $"{longName} cannot be repeated.";
                        return false;
                    }

                    wasPresent = true;
                    value = inlineValue;
                    continue;
                }

                if (!string.Equals(arg, longName, StringComparison.Ordinal) &&
                    !string.Equals(arg, editorName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (wasPresent)
                {
                    error = $"{longName} cannot be repeated.";
                    return false;
                }

                wasPresent = true;
                if (i + 1 < args.Count)
                {
                    value = args[i + 1] ?? string.Empty;
                }
            }

            return true;
        }

        private static bool TryReadInlineArgumentValue(string arg, string name, out string value)
        {
            value = string.Empty;
            var prefix = name + "=";
            if (!arg.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            value = arg.Substring(prefix.Length);
            return true;
        }
    }
}
