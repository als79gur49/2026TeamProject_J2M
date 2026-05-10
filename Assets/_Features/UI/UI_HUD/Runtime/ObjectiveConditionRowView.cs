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
        [SerializeField] private CanvasGroup _canvasGroup;

        public void Bind(ObjectiveConditionHudViewModel viewModel)
        {
            ValidateAuthoredStructureOrThrow();
            _titleText.text = viewModel.Text;
            _titleText.color = viewModel.IsSatisfied
                ? new Color(0.56f, 0.9f, 0.66f, 1.0f)
                : Color.white;
            _checkIcon.color = viewModel.IsSatisfied
                ? new Color(0.56f, 0.9f, 0.66f, 1.0f)
                : new Color(1.0f, 1.0f, 1.0f, 0.28f);
            _canvasGroup.alpha = viewModel.IsSatisfied ? 0.86f : 1.0f;
            transform.localScale = Vector3.one;
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_checkIcon, nameof(_checkIcon));
            RequireReference(_titleText, nameof(_titleText));
            RequireReference(_canvasGroup, nameof(_canvasGroup));
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
