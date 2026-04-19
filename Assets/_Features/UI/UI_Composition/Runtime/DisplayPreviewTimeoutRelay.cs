using System;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    internal sealed class DisplayPreviewTimeoutRelay : MonoBehaviour
    {
        private Action onElapsed;
        private double deadline;
        private Func<double> timeProvider;

        public bool IsArmed => onElapsed != null;

        private void Awake()
        {
            timeProvider ??= GetTimeNow;
        }

        public void Arm(double durationSeconds, Action callback)
        {
            onElapsed = callback;
            deadline = (timeProvider ??= GetTimeNow).Invoke() + Math.Max(0d, durationSeconds);
        }

        public void Cancel()
        {
            onElapsed = null;
            deadline = 0d;
        }

        private void Update()
        {
            if (onElapsed == null || (timeProvider ??= GetTimeNow).Invoke() < deadline)
            {
                return;
            }

            var callback = onElapsed;
            Cancel();
            callback?.Invoke();
        }

        private static double GetTimeNow()
        {
            return Time.realtimeSinceStartupAsDouble;
        }

        internal void SetTimeProviderForTesting(Func<double> provider)
        {
            timeProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }
    }
}
