using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class ObjectiveConditionRowView : MonoBehaviour
    {
        [SerializeField] private Image _checkIcon;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private TMP_Text _badgeText;
        [SerializeField] private CanvasGroup _canvasGroup;

        private Sequence _sequence;

        public void Bind(
            ObjectiveConditionHudViewModel viewModel,
            HudAnimationSettings settings)
        {
            EnsureBuilt();
            KillSequence();
            _titleText.text = viewModel.Title;
            _titleText.color = viewModel.IsSatisfied
                ? new Color(0.56f, 0.9f, 0.66f, 1.0f)
                : Color.white;
            _progressText.text = viewModel.ProgressText;
            _badgeText.text = ResolveBadgeText(viewModel);
            _checkIcon.color = viewModel.IsSatisfied
                ? new Color(0.56f, 0.9f, 0.66f, 1.0f)
                : new Color(1.0f, 1.0f, 1.0f, 0.28f);
            _canvasGroup.alpha = viewModel.IsSatisfied ? 0.86f : 1.0f;
            transform.localScale = Vector3.one;

            if (viewModel.JustSatisfied && !settings.ReduceMotion)
            {
                _sequence = DOTween.Sequence()
                    .SetUpdate(settings.UseUnscaledTime)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
                _sequence.Append(transform.DOPunchScale(Vector3.one * 0.06f, 0.22f, 6, 0.7f));
                _sequence.Join(_canvasGroup.DOFade(1.0f, 0.08f));
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

            _checkIcon = _checkIcon != null ? _checkIcon : CreateImage("CheckIcon");
            _titleText = _titleText != null ? _titleText : CreateText("TitleText", 14, TextAlignmentOptions.Left);
            _progressText = _progressText != null ? _progressText : CreateText("ProgressText", 12, TextAlignmentOptions.Right);
            _badgeText = _badgeText != null ? _badgeText : CreateText("RequiredBadge", 11, TextAlignmentOptions.Center);
        }

        private Image CreateImage(string childName)
        {
            var child = new GameObject(childName, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(transform, false);
            var rect = (RectTransform)child.transform;
            rect.sizeDelta = new Vector2(14.0f, 14.0f);
            var image = child.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text CreateText(
            string childName,
            int fontSize,
            TextAlignmentOptions alignment)
        {
            var child = new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(transform, false);
            var label = child.GetComponent<TMP_Text>();
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static string ResolveBadgeText(ObjectiveConditionHudViewModel viewModel)
        {
            if (viewModel.Role == ObjectiveConditionHudRole.Challenge)
            {
                return "CHALLENGE";
            }

            return viewModel.Required ? "REQ" : "OPT";
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
    }
}
