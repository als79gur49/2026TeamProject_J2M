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

        private int _displayedCount = -1;

        public bool IsReady => countLabel != null && canvasGroup != null && anchor != null;

        public void Bind(Transform target, float surfaceInsetDistance, Camera outputCamera)
        {
            EnsureReady();
            anchor.Initialize(target, surfaceInsetDistance, outputCamera);
        }

        public void ShowCount(int count)
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
                countLabel.text = string.Concat("×", count.ToString(CultureInfo.InvariantCulture));
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
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
                    $"{nameof(GameplayPlayerActionCountView)} requires a count label, CanvasGroup, and player-action anchor.");
            }
        }
    }
}
