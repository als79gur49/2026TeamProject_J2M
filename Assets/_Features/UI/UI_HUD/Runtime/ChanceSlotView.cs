using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class ChanceSlotView : MonoBehaviour
    {
        private static readonly Color FilledColor = new Color(0.91f, 0.22f, 0.36f, 1.0f);
        private static readonly Color EmptyColor = new Color(0.25f, 0.29f, 0.36f, 0.75f);
        private static readonly Color GlowColor = new Color(1.0f, 0.24f, 0.34f, 0.0f);

        [SerializeField] private Image _filledIcon;
        [SerializeField] private Image _emptyIcon;
        [SerializeField] private Image _glow;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private GameObject _lossVfxRoot;
        [SerializeField] private GameObject _gainVfxRoot;

        private Sequence _sequence;

        public void Bind(
            ChanceSlotViewModel viewModel,
            ChanceChangeAnimationHint animationHint,
            HudAnimationSettings settings)
        {
            EnsureBuilt();
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

        private void OnDisable()
        {
            KillSequence();
        }

        private void OnDestroy()
        {
            KillSequence();
        }

        private void EnsureBuilt()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            }

            _emptyIcon = _emptyIcon != null ? _emptyIcon : CreateImage("EmptyIcon", EmptyColor);
            _filledIcon = _filledIcon != null ? _filledIcon : CreateImage("FilledIcon", FilledColor);
            _glow = _glow != null ? _glow : CreateImage("Glow", GlowColor);
            _glow.transform.SetAsFirstSibling();
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

        private void ApplyImmediate(ChanceSlotViewModel viewModel)
        {
            KillSequence();
            transform.localScale = Vector3.one;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1.0f;
            }

            if (_filledIcon != null)
            {
                _filledIcon.color = FilledColor;
                _filledIcon.gameObject.SetActive(viewModel.IsFilled);
                _filledIcon.transform.localScale = Vector3.one;
            }

            if (_emptyIcon != null)
            {
                _emptyIcon.color = EmptyColor;
                _emptyIcon.gameObject.SetActive(!viewModel.IsFilled);
            }

            if (_glow != null)
            {
                _glow.color = viewModel.IsLastChanceSlot
                    ? new Color(1.0f, 0.24f, 0.34f, 0.32f)
                    : GlowColor;
                _glow.transform.localScale = Vector3.one;
            }
        }

        private void PlayLoss(
            ChanceSlotViewModel viewModel,
            HudAnimationSettings settings)
        {
            KillSequence();
            ToggleVfx(_lossVfxRoot);
            if (_filledIcon != null)
            {
                _filledIcon.gameObject.SetActive(true);
            }

            if (_emptyIcon != null)
            {
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
                _emptyIcon.color = new Color(EmptyColor.r, EmptyColor.g, EmptyColor.b, 0.0f);
                _sequence.Join(_emptyIcon.DOFade(EmptyColor.a, 0.18f));
            }

            _sequence.OnComplete(() => ApplyImmediate(viewModel));
        }

        private void PlayGain(
            ChanceSlotViewModel viewModel,
            HudAnimationSettings settings)
        {
            KillSequence();
            ToggleVfx(_gainVfxRoot);
            if (_emptyIcon != null)
            {
                _emptyIcon.gameObject.SetActive(true);
            }

            if (_filledIcon != null)
            {
                _filledIcon.gameObject.SetActive(true);
                _filledIcon.color = new Color(FilledColor.r, FilledColor.g, FilledColor.b, 0.0f);
                _filledIcon.transform.localScale = Vector3.one * 0.7f;
            }

            if (_glow != null)
            {
                _glow.color = new Color(1.0f, 0.42f, 0.54f, 0.45f);
                _glow.transform.localScale = Vector3.one * 0.75f;
            }

            _sequence = DOTween.Sequence()
                .SetUpdate(settings.UseUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            if (_filledIcon != null)
            {
                _sequence.Append(_filledIcon.DOFade(FilledColor.a, 0.16f));
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
    }
}
