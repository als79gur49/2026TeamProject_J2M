using DG.Tweening;
using System;
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
            ValidateAuthoredStructureOrThrow();
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

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_checkIcon, nameof(_checkIcon));
            RequireReference(_titleText, nameof(_titleText));
            RequireReference(_progressText, nameof(_progressText));
            RequireReference(_badgeText, nameof(_badgeText));
            RequireReference(_canvasGroup, nameof(_canvasGroup));
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

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveConditionRowView)} is missing authored reference '{fieldName}'.");
            }
        }
    }
}
