using System;
using Game.Feature.UI.Application;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    internal sealed class DisplayPreviewTimeoutRelay : MonoBehaviour
    {
        private Action onElapsed;
        private double deadline;
        private Func<double> timeProvider;
        private int lastVisibleSeconds;
        private int totalSeconds;

        public event Action<DisplayPreviewCountdownSnapshot> CountdownChanged;

        public bool IsArmed => onElapsed != null;

        private void Awake()
        {
            timeProvider ??= GetTimeNow;
        }

        public void Arm(double durationSeconds, Action callback)
        {
            onElapsed = callback;
            deadline = (timeProvider ??= GetTimeNow).Invoke() + Math.Max(0d, durationSeconds);
            totalSeconds = DisplayPreviewCountdownSnapshot.ComputeVisibleSeconds(durationSeconds, durationSeconds);
            lastVisibleSeconds = totalSeconds;
        }

        public void Cancel()
        {
            if (onElapsed == null)
            {
                deadline = 0d;
                totalSeconds = 0;
                lastVisibleSeconds = 0;
                return;
            }

            onElapsed = null;
            deadline = 0d;
            totalSeconds = 0;
            lastVisibleSeconds = 0;
            CountdownChanged?.Invoke(DisplayPreviewCountdownSnapshot.Inactive);
        }

        public DisplayPreviewCountdownSnapshot ReadCurrentSnapshot()
        {
            if (onElapsed == null)
            {
                return DisplayPreviewCountdownSnapshot.Inactive;
            }

            var remainingSeconds = Math.Max(0d, deadline - (timeProvider ??= GetTimeNow).Invoke());
            return DisplayPreviewCountdownSnapshot.Create(remainingSeconds, totalSeconds);
        }

        private void Update()
        {
            if (onElapsed == null)
            {
                return;
            }

            var now = (timeProvider ??= GetTimeNow).Invoke();
            if (now >= deadline)
            {
                var callback = onElapsed;
                Cancel();
                callback?.Invoke();
                return;
            }

            var snapshot = ReadCurrentSnapshot();
            if (snapshot.IsActive && snapshot.RemainingSeconds != lastVisibleSeconds)
            {
                lastVisibleSeconds = snapshot.RemainingSeconds;
                CountdownChanged?.Invoke(snapshot);
            }
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

    [DisallowMultipleComponent]
    internal sealed class DisplayStatusTransientRelay : MonoBehaviour
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
            onElapsed = callback ?? throw new ArgumentNullException(nameof(callback));
            deadline = (timeProvider ??= GetTimeNow).Invoke() + Math.Max(0d, durationSeconds);
        }

        public void Cancel()
        {
            onElapsed = null;
            deadline = 0d;
        }

        private void Update()
        {
            if (onElapsed == null)
            {
                return;
            }

            if ((timeProvider ??= GetTimeNow).Invoke() < deadline)
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
