using DG.Tweening;
using UnityEngine;

namespace Game.Feature.UI.Screens
{
    internal static class ScreenEnterTweenUtility
    {
        private const float ScreenEnterFadeDurationSeconds = 0.18f;
        private const Ease ScreenEnterFadeEase = Ease.OutCubic;
        private const bool ScreenEnterUseUnscaledTime = true;

        internal static CanvasGroup EnsureCanvasGroup(GameObject target, CanvasGroup currentCanvasGroup)
        {
            if (currentCanvasGroup != null)
            {
                return currentCanvasGroup;
            }

            if (target == null)
            {
                return null;
            }

            if (target.TryGetComponent<CanvasGroup>(out var canvasGroup))
            {
                return canvasGroup;
            }

            return target.AddComponent<CanvasGroup>();
        }

        internal static void Kill(ref Tween tween)
        {
            if (tween == null)
            {
                return;
            }

            tween.Kill();
            tween = null;
        }

        internal static void RestoreAlpha(CanvasGroup canvasGroup, float alpha)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = alpha;
        }

        internal static Tween PlayEnterFade(CanvasGroup canvasGroup, out float restAlpha)
        {
            restAlpha = 1f;
            if (canvasGroup == null)
            {
                return null;
            }

            restAlpha = canvasGroup.alpha;
            canvasGroup.alpha = 0f;
            return canvasGroup
                .DOFade(restAlpha, ScreenEnterFadeDurationSeconds)
                .SetEase(ScreenEnterFadeEase)
                .SetUpdate(ScreenEnterUseUnscaledTime);
        }
    }
}
