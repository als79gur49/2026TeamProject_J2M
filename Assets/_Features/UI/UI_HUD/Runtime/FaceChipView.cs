using DG.Tweening;
using System;
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
            ValidateAuthoredStructureOrThrow();
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

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_background, nameof(_background));
            RequireReference(_faceNameText, nameof(_faceNameText));
            RequireReference(_activeGlow, nameof(_activeGlow));
            RequireReference(_canvasGroup, nameof(_canvasGroup));
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

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(FaceChipView)} is missing authored reference '{fieldName}'.");
            }
        }
    }
}
