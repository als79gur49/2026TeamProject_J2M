using System;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.ViewShared
{
    public readonly struct ResultDimVisualSnapshot
    {
        public ResultDimVisualSnapshot(
            Color finalColor,
            Color opaqueColor,
            bool isFullStretch,
            bool raycastTarget)
        {
            ValidateColor(finalColor, nameof(finalColor));
            ValidateColor(opaqueColor, nameof(opaqueColor));
            if (!Mathf.Approximately(opaqueColor.a, 1f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(opaqueColor),
                    opaqueColor.a,
                    "Result Dim opaque color alpha must be exactly one.");
            }

            if (!SameRgb(finalColor, opaqueColor))
            {
                throw new ArgumentException(
                    "Result Dim final and opaque colors must use the same RGB.",
                    nameof(opaqueColor));
            }

            if (!isFullStretch)
            {
                throw new ArgumentException(
                    "Result Dim v1 requires a full-stretch visual.",
                    nameof(isFullStretch));
            }

            if (raycastTarget)
            {
                throw new ArgumentException(
                    "Result Dim is visual-only and cannot be a raycast target.",
                    nameof(raycastTarget));
            }

            FinalColor = finalColor;
            OpaqueColor = opaqueColor;
            IsFullStretch = isFullStretch;
            RaycastTarget = raycastTarget;
        }

        public Color FinalColor { get; }

        public Color OpaqueColor { get; }

        public bool IsFullStretch { get; }

        public bool RaycastTarget { get; }

        private static void ValidateColor(Color color, string parameterName)
        {
            if (!IsFinite(color.r) ||
                !IsFinite(color.g) ||
                !IsFinite(color.b) ||
                !IsFinite(color.a) ||
                color.r < 0f || color.r > 1f ||
                color.g < 0f || color.g > 1f ||
                color.b < 0f || color.b > 1f ||
                color.a < 0f || color.a > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    color,
                    "Result Dim colors must contain finite normalized channels.");
            }
        }

        private static bool SameRgb(Color left, Color right)
        {
            return left.r.Equals(right.r) &&
                   left.g.Equals(right.g) &&
                   left.b.Equals(right.b);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct ResultTransitionRuntimeStyle
    {
        public ResultTransitionRuntimeStyle(
            float resultHandoffHoldDuration,
            float resultHandoffFadeDuration,
            TerminalIrisEasing resultHandoffEasing,
            float resultContentEntranceDelay,
            float contentEntranceDuration,
            TerminalIrisEasing resultContentEntranceEasing,
            float resultExitCoverFadeDuration,
            TerminalIrisEasing resultExitCoverEasing,
            float pixelComparisonTolerance,
            float brightnessMeanDeltaTolerance,
            float brightnessP99DeltaTolerance)
        {
            ValidateNonNegative(resultHandoffHoldDuration, nameof(resultHandoffHoldDuration));
            ValidatePositive(resultHandoffFadeDuration, nameof(resultHandoffFadeDuration));
            ValidateNonNegative(resultContentEntranceDelay, nameof(resultContentEntranceDelay));
            ValidatePositive(contentEntranceDuration, nameof(contentEntranceDuration));
            ValidatePositive(resultExitCoverFadeDuration, nameof(resultExitCoverFadeDuration));
            ValidateNonNegative(pixelComparisonTolerance, nameof(pixelComparisonTolerance));
            ValidateNonNegative(brightnessMeanDeltaTolerance, nameof(brightnessMeanDeltaTolerance));
            ValidateNonNegative(brightnessP99DeltaTolerance, nameof(brightnessP99DeltaTolerance));
            ValidateEasing(resultHandoffEasing, nameof(resultHandoffEasing));
            ValidateEasing(resultContentEntranceEasing, nameof(resultContentEntranceEasing));
            ValidateEasing(resultExitCoverEasing, nameof(resultExitCoverEasing));

            ResultHandoffHoldDuration = resultHandoffHoldDuration;
            ResultHandoffFadeDuration = resultHandoffFadeDuration;
            ResultHandoffEasing = resultHandoffEasing;
            ResultContentEntranceDelay = resultContentEntranceDelay;
            ContentEntranceDuration = contentEntranceDuration;
            ResultContentEntranceEasing = resultContentEntranceEasing;
            ResultExitCoverFadeDuration = resultExitCoverFadeDuration;
            ResultExitCoverEasing = resultExitCoverEasing;
            PixelComparisonTolerance = pixelComparisonTolerance;
            BrightnessMeanDeltaTolerance = brightnessMeanDeltaTolerance;
            BrightnessP99DeltaTolerance = brightnessP99DeltaTolerance;
        }

        public float ResultHandoffHoldDuration { get; }
        public float ResultHandoffFadeDuration { get; }
        public TerminalIrisEasing ResultHandoffEasing { get; }
        public float ResultContentEntranceDelay { get; }
        public float ContentEntranceDuration { get; }
        public TerminalIrisEasing ResultContentEntranceEasing { get; }
        public float ResultExitCoverFadeDuration { get; }
        public TerminalIrisEasing ResultExitCoverEasing { get; }
        public float PixelComparisonTolerance { get; }
        public float BrightnessMeanDeltaTolerance { get; }
        public float BrightnessP99DeltaTolerance { get; }

        public float EvaluateResultHandoff(float progress)
        {
            return TerminalIrisEasingUtility.Evaluate(ResultHandoffEasing, progress);
        }

        public float EvaluateResultContentEntrance(float progress)
        {
            return TerminalIrisEasingUtility.Evaluate(ResultContentEntranceEasing, progress);
        }

        public float EvaluateResultExitCover(float progress)
        {
            return TerminalIrisEasingUtility.Evaluate(ResultExitCoverEasing, progress);
        }

        private static void ValidatePositive(float value, string parameterName)
        {
            ValidateFinite(value, parameterName);
            if (value <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Result transition duration must be greater than zero.");
            }
        }

        private static void ValidateNonNegative(float value, string parameterName)
        {
            ValidateFinite(value, parameterName);
            if (value < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Result transition value cannot be negative.");
            }
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Result transition values must be finite.");
            }
        }

        private static void ValidateEasing(TerminalIrisEasing easing, string parameterName)
        {
            if (!Enum.IsDefined(typeof(TerminalIrisEasing), easing))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    easing,
                    "Result transition easing must be a defined value.");
            }
        }
    }
}
