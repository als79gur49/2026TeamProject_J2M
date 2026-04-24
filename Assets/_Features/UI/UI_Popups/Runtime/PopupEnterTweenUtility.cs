using DG.Tweening;
using UnityEngine;

namespace Game.Feature.UI.Popups
{
    internal static class PopupEnterTweenUtility
    {
        private const float ModalEnterDurationSeconds = 0.20f;
        private const float TooltipEnterDurationSeconds = 0.12f;
        private const float ModalStartScaleMultiplier = 0.96f;

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

        internal static void RestoreScale(Transform targetTransform, Vector3 scale)
        {
            if (targetTransform == null)
            {
                return;
            }

            targetTransform.localScale = scale;
        }

        internal static Tween PlayModalEnter(
            CanvasGroup canvasGroup,
            Transform targetTransform,
            out float restAlpha,
            out Vector3 restScale)
        {
            restAlpha = 1f;
            restScale = Vector3.one;

            if (canvasGroup != null)
            {
                restAlpha = canvasGroup.alpha;
                canvasGroup.alpha = 0f;
            }

            if (targetTransform != null)
            {
                restScale = targetTransform.localScale;
                targetTransform.localScale = restScale * ModalStartScaleMultiplier;
            }

            if (canvasGroup == null && targetTransform == null)
            {
                return null;
            }

            var sequence = DOTween.Sequence().SetUpdate(true);
            if (canvasGroup != null)
            {
                sequence.Join(canvasGroup.DOFade(restAlpha, ModalEnterDurationSeconds).SetEase(Ease.OutCubic));
            }

            if (targetTransform != null)
            {
                sequence.Join(targetTransform.DOScale(restScale, ModalEnterDurationSeconds).SetEase(Ease.OutCubic));
            }

            return sequence;
        }

        internal static Tween PlayTooltipEnter(CanvasGroup canvasGroup, out float restAlpha)
        {
            restAlpha = 1f;
            if (canvasGroup == null)
            {
                return null;
            }

            restAlpha = canvasGroup.alpha;
            canvasGroup.alpha = 0f;
            return canvasGroup
                .DOFade(restAlpha, TooltipEnterDurationSeconds)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }
    }
}
