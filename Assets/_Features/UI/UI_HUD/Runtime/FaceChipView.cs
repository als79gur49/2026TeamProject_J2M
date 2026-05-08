using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class FaceChipView : MonoBehaviour
    {
        private static readonly Color ActiveColor = new Color(0.22f, 0.58f, 0.92f, 1.0f);
        private static readonly Color InactiveColor = new Color(0.12f, 0.15f, 0.2f, 0.72f);
        private static readonly Color EndpointColor = new Color(0.32f, 0.42f, 0.58f, 0.9f);

        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _faceNameText;
        [SerializeField] private Image _activeGlow;
        [SerializeField] private CanvasGroup _canvasGroup;

        private Tween _pulseTween;

        public void Bind(
            FaceChipViewModel viewModel,
            bool pulse,
            HudAnimationSettings settings)
        {
            EnsureBuilt();
            KillPulse();
            _faceNameText.text = viewModel.Label;
            _canvasGroup.alpha = viewModel.IsActive ? 1.0f : 0.62f;
            _background.color = viewModel.IsActive
                ? ActiveColor
                : (viewModel.IsTransitionEndpoint ? EndpointColor : InactiveColor);
            _activeGlow.gameObject.SetActive(viewModel.IsActive || viewModel.IsTransitionEndpoint);
            _activeGlow.color = viewModel.IsActive
                ? new Color(0.5f, 0.78f, 1.0f, 0.36f)
                : new Color(0.5f, 0.78f, 1.0f, 0.18f);
            transform.localScale = Vector3.one;

            if (pulse && !settings.ReduceMotion)
            {
                _pulseTween = transform
                    .DOPunchScale(Vector3.one * 0.12f, 0.28f, 8, 0.7f)
                    .SetUpdate(settings.UseUnscaledTime)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
        }

        private void OnDisable()
        {
            KillPulse();
        }

        private void OnDestroy()
        {
            KillPulse();
        }

        private void EnsureBuilt()
        {
            if (_canvasGroup == null)
            {
                var canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }

                _canvasGroup = canvasGroup;
            }

            _background = _background != null ? _background : CreateImage("Background", InactiveColor);
            _activeGlow = _activeGlow != null ? _activeGlow : CreateImage("ActiveGlow", Color.clear);
            _faceNameText = _faceNameText != null ? _faceNameText : CreateText("FaceNameText");
        }

        private Image CreateImage(string childName, Color color)
        {
            var child = new GameObject(childName, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(transform, false);
            var rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text CreateText(string childName)
        {
            var child = new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(transform, false);
            var rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var label = child.GetComponent<TMP_Text>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 14;
            label.raycastTarget = false;
            return label;
        }

        private void KillPulse()
        {
            if (_pulseTween == null)
            {
                return;
            }

            _pulseTween.Kill(false);
            _pulseTween = null;
        }
    }
}
