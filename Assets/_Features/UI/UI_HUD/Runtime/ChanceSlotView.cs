using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class ChanceSlotView : MonoBehaviour
    {
        [SerializeField] private Image _filledIcon;
        [SerializeField] private Image _emptyIcon;
        [SerializeField] private Image _glow;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private GameObject _lossVfxRoot;
        [SerializeField] private GameObject _gainVfxRoot;

        private Sequence _sequence;
        private bool _hasAuthoredColors;
        private Color _authoredFilledColor;
        private Color _authoredEmptyColor;
        private Color _authoredGlowColor;

        public void Bind(
            ChanceSlotViewModel viewModel,
            ChanceChangeAnimationHint animationHint,
            HudAnimationSettings settings)
        {
            ValidateAuthoredStructureOrThrow();
            EnsureAuthoredColorsCached();
            var shouldAnimate = animationHint.Kind != ChanceChangeKind.None &&
                animationHint.SequenceId > 0 &&
                ContainsSlot(animationHint, viewModel.Index);

            if (!shouldAnimate || settings.ReduceMotion)
            {
                ApplyImmediate(viewModel);
                return;
            }

            switch (animationHint.Kind)
            {
                case ChanceChangeKind.Gained:
                    PlayGain(viewModel, settings);
                    break;
                case ChanceChangeKind.Lost:
                case ChanceChangeKind.LastChanceEntered:
                    PlayLoss(viewModel, settings);
                    break;
                default:
                    ApplyImmediate(viewModel);
                    break;
            }
        }

        private void Awake()
        {
            TryCacheAuthoredColors();
        }

        private void OnDisable()
        {
            KillSequence();
        }

        private void OnDestroy()
        {
            KillSequence();
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_filledIcon, nameof(_filledIcon));
            RequireReference(_emptyIcon, nameof(_emptyIcon));
            RequireReference(_glow, nameof(_glow));
            RequireReference(_canvasGroup, nameof(_canvasGroup));
        }

        private void ApplyImmediate(ChanceSlotViewModel viewModel)
        {
            EnsureAuthoredColorsCached();
            KillSequence();
            transform.localScale = Vector3.one;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1.0f;
            }

            if (_filledIcon != null)
            {
                _filledIcon.color = _authoredFilledColor;
                _filledIcon.gameObject.SetActive(viewModel.IsFilled);
                _filledIcon.transform.localScale = Vector3.one;
            }

            if (_emptyIcon != null)
            {
                _emptyIcon.color = _authoredEmptyColor;
                _emptyIcon.gameObject.SetActive(!viewModel.IsFilled);
            }

            if (_glow != null)
            {
                _glow.color = _authoredGlowColor;
                _glow.transform.localScale = Vector3.one;
            }
        }

        private void PlayLoss(
            ChanceSlotViewModel viewModel,
            HudAnimationSettings settings)
        {
            KillSequence();
            EnsureAuthoredColorsCached();
            ToggleVfx(_lossVfxRoot);
            if (_filledIcon != null)
            {
                _filledIcon.color = _authoredFilledColor;
                _filledIcon.gameObject.SetActive(true);
            }

            if (_emptyIcon != null)
            {
                _emptyIcon.color = WithAlpha(_authoredEmptyColor, 0.0f);
                _emptyIcon.gameObject.SetActive(true);
            }

            _sequence = DOTween.Sequence()
                .SetUpdate(settings.UseUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            _sequence.Append(transform.DOPunchScale(Vector3.one * 0.18f, 0.22f, 8, 0.7f));
            if (_filledIcon != null)
            {
                _sequence.Join(_filledIcon.DOFade(0.0f, 0.16f));
            }

            if (_emptyIcon != null)
            {
                _sequence.Join(_emptyIcon.DOFade(_authoredEmptyColor.a, 0.18f));
            }

            _sequence.OnComplete(() => ApplyImmediate(viewModel));
        }

        private void PlayGain(
            ChanceSlotViewModel viewModel,
            HudAnimationSettings settings)
        {
            KillSequence();
            EnsureAuthoredColorsCached();
            ToggleVfx(_gainVfxRoot);
            if (_emptyIcon != null)
            {
                _emptyIcon.color = _authoredEmptyColor;
                _emptyIcon.gameObject.SetActive(true);
            }

            if (_filledIcon != null)
            {
                _filledIcon.gameObject.SetActive(true);
                _filledIcon.color = WithAlpha(_authoredFilledColor, 0.0f);
                _filledIcon.transform.localScale = Vector3.one * 0.7f;
            }

            if (_glow != null)
            {
                _glow.color = _authoredGlowColor;
                _glow.transform.localScale = Vector3.one * 0.75f;
            }

            _sequence = DOTween.Sequence()
                .SetUpdate(settings.UseUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            if (_filledIcon != null)
            {
                _sequence.Append(_filledIcon.DOFade(_authoredFilledColor.a, 0.16f));
                _sequence.Join(_filledIcon.transform.DOScale(1.0f, 0.2f).SetEase(Ease.OutBack));
            }

            if (_glow != null)
            {
                _sequence.Join(_glow.transform.DOScale(1.45f, 0.24f).SetEase(Ease.OutQuad));
                _sequence.Join(_glow.DOFade(0.0f, 0.28f));
            }

            _sequence.OnComplete(() => ApplyImmediate(viewModel));
        }

        private void KillSequence()
        {
            if (_sequence == null)
            {
                return;
            }

            _sequence.Kill(false);
            _sequence = null;
        }

        private static bool ContainsSlot(
            ChanceChangeAnimationHint hint,
            int slotIndex)
        {
            var indices = hint.ChangedSlotIndices;
            if (indices == null)
            {
                return false;
            }

            for (var i = 0; i < indices.Count; i++)
            {
                if (indices[i] == slotIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ToggleVfx(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(false);
            root.SetActive(true);
        }

        private void EnsureAuthoredColorsCached()
        {
            if (_hasAuthoredColors)
            {
                return;
            }

            ValidateAuthoredStructureOrThrow();
            TryCacheAuthoredColors();
        }

        private void TryCacheAuthoredColors()
        {
            if (_filledIcon == null || _emptyIcon == null || _glow == null)
            {
                return;
            }

            _authoredFilledColor = _filledIcon.color;
            _authoredEmptyColor = _emptyIcon.color;
            _authoredGlowColor = _glow.color;
            _hasAuthoredColors = true;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(ChanceSlotView)} is missing authored reference '{fieldName}'.");
            }
        }
    }
}
