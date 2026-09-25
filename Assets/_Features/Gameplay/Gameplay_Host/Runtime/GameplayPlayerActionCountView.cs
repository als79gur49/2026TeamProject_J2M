using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayPlayerActionCountView : MonoBehaviour
    {
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameplayPlayerActionCountAnchor anchor;
        [SerializeField] private GameplayPlayerActionCountEffectDriver effectDriver;

        private int _displayedCount = -1;

        public bool IsReady =>
            countLabel != null &&
            canvasGroup != null &&
            anchor != null &&
            effectDriver != null &&
            effectDriver.IsReady;

        public void Bind(
            Transform target,
            float surfaceInsetDistance,
            float cameraRightOffsetDistance,
            Camera outputCamera)
        {
            EnsureReady();
            anchor.Initialize(target, surfaceInsetDistance, cameraRightOffsetDistance, outputCamera);
        }

        public void ShowCount(int count)
        {
            ShowCountCore(count, playIncrementEffect: false);
        }

        public void ShowCountWithIncrementEffect(int count)
        {
            ShowCountCore(count, playIncrementEffect: true);
        }

        internal void AdvanceEffect(float deltaTime)
        {
            effectDriver.Advance(deltaTime);
        }

        private void ShowCountCore(int count, bool playIncrementEffect)
        {
            EnsureReady();
            if (count <= 0)
            {
                Hide();
                return;
            }

            if (_displayedCount != count)
            {
                _displayedCount = count;
                countLabel.text = string.Concat("x", count.ToString(CultureInfo.InvariantCulture));
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (playIncrementEffect)
            {
                effectDriver.PlayIncrement();
            }
        }

        public void SetAlpha(float alpha)
        {
            EnsureReady();
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            effectDriver?.ResetVisuals();

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void EnsureReady()
        {
            if (!IsReady)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameplayPlayerActionCountView)} requires a count label, CanvasGroup, player-action anchor, and ready effect driver.");
            }
        }
    }
}
