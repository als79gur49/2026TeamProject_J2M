using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.ViewShared
{
    [Serializable]
    public sealed class UiSelectableButtonSlot
    {
        public UiSelectableButtonSlot()
        {
        }

        public UiSelectableButtonSlot(Button button, Image selectionFrame, MonoBehaviour selectionFeedback = null)
        {
            Button = button;
            SelectionFrame = selectionFrame;
            SelectionFeedback = selectionFeedback;
        }

        public Button Button;

        public Image SelectionFrame;

        public MonoBehaviour SelectionFeedback;

        public bool IsConfigured => Button != null && SelectionFrame != null;

        public IUiSelectionFeedback ResolveSelectionFeedback()
        {
            var explicitFeedback = GetLiveFeedback(SelectionFeedback as IUiSelectionFeedback);
            if (explicitFeedback != null)
            {
                return explicitFeedback;
            }

            if (Button != null && Button.TryGetComponent<IUiSelectionFeedback>(out var buttonFeedback))
            {
                buttonFeedback = GetLiveFeedback(buttonFeedback);
                if (buttonFeedback != null)
                {
                    return buttonFeedback;
                }
            }

            return SelectionFrame != null
                ? GetLiveFeedback(
                    SelectionFrame.GetComponentInParent<IUiSelectionFeedback>(includeInactive: true))
                : null;
        }

        private static IUiSelectionFeedback GetLiveFeedback(IUiSelectionFeedback feedback)
        {
            return feedback is UnityEngine.Object unityObject && unityObject == null
                ? null
                : feedback;
        }
    }
}
