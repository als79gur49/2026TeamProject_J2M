using System;
using Game.Feature.Stages;
using Game.Feature.UI.ViewShared;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [CreateAssetMenu(
        fileName = "ResultTransitionVisualStyle",
        menuName = "Game/UI/Result Transition Visual Style")]
    public sealed class ResultTransitionVisualStyle : ScriptableObject
    {
        [SerializeField] private Color _baseDimColor = new(0f, 0.25133762f, 0.4811321f, 0.9411765f);
        [SerializeField] private float _resultHandoffHoldDuration = 0.05f;
        [SerializeField] private float _resultHandoffFadeDuration = 0.25f;
        [SerializeField] private TerminalIrisEasing _resultHandoffEasing = TerminalIrisEasing.SmoothStep;
        [SerializeField] private float _resultContentEntranceDelay = 0.06f;
        [SerializeField] private float _contentEntranceDuration = 0.2f;
        [SerializeField] private TerminalIrisEasing _resultContentEntranceEasing = TerminalIrisEasing.SmoothStep;
        [SerializeField] private float _resultExitCoverFadeDuration = 0.2f;
        [SerializeField] private TerminalIrisEasing _resultExitCoverEasing = TerminalIrisEasing.SmoothStep;
        [SerializeField] private float _pixelComparisonTolerance = 1f / 255f;
        [SerializeField] private float _brightnessMeanDeltaTolerance = 0.005f;
        [SerializeField] private float _brightnessP99DeltaTolerance = 0.03f;

        public Color ClosedCoverColor => CreateDimSnapshot().OpaqueColor;

        public Color ResultBackdropColor => CreateDimSnapshot().FinalColor;

        public Color PersistentCoverColor => CreateDimSnapshot().OpaqueColor;

        public Color EntryIrisColor => CreateDimSnapshot().OpaqueColor;

        public float PixelComparisonTolerance => CreateRuntimeSnapshot().PixelComparisonTolerance;

        public float ContentEntranceDuration => CreateRuntimeSnapshot().ContentEntranceDuration;

        public ResultDimVisualSnapshot CreateDimSnapshot()
        {
            ValidateColor(_baseDimColor, nameof(_baseDimColor));
            var opaqueColor = _baseDimColor;
            opaqueColor.a = 1f;
            return new ResultDimVisualSnapshot(
                _baseDimColor,
                opaqueColor,
                isFullStretch: true,
                raycastTarget: false);
        }

        public ResultTransitionRuntimeStyle CreateRuntimeSnapshot()
        {
            return new ResultTransitionRuntimeStyle(
                _resultHandoffHoldDuration,
                _resultHandoffFadeDuration,
                _resultHandoffEasing,
                _resultContentEntranceDelay,
                _contentEntranceDuration,
                _resultContentEntranceEasing,
                _resultExitCoverFadeDuration,
                _resultExitCoverEasing,
                _pixelComparisonTolerance,
                _brightnessMeanDeltaTolerance,
                _brightnessP99DeltaTolerance);
        }

        internal void ValidateOrThrow()
        {
            _ = CreateDimSnapshot();
            _ = CreateRuntimeSnapshot();
        }

        private static void ValidateColor(Color color, string fieldName)
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
                throw new InvalidOperationException(
                    $"{nameof(ResultTransitionVisualStyle)}.{fieldName} must contain finite normalized channels.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
