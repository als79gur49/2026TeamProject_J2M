using DG.Tweening;
using UnityEngine;

namespace Game.Feature.UI.Popups
{
    internal static class PopupEnterTweenUtility
    {
        private const float ModalPopupEnterDurationSeconds = 0.20f;
        private const float ModalPopupEnterStartScaleMultiplier = 0.96f;
        private const Ease ModalPopupEnterEase = Ease.OutCubic;
        private const bool PopupEnterUseUnscaledTime = true;

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
                targetTransform.localScale = restScale * ModalPopupEnterStartScaleMultiplier;
            }

            if (canvasGroup == null && targetTransform == null)
            {
                return null;
            }

            var sequence = DOTween.Sequence().SetUpdate(PopupEnterUseUnscaledTime);
            if (canvasGroup != null)
            {
                sequence.Join(canvasGroup.DOFade(restAlpha, ModalPopupEnterDurationSeconds).SetEase(ModalPopupEnterEase));
            }

            if (targetTransform != null)
            {
                sequence.Join(targetTransform.DOScale(restScale, ModalPopupEnterDurationSeconds).SetEase(ModalPopupEnterEase));
            }

            return sequence;
        }

    }
}
