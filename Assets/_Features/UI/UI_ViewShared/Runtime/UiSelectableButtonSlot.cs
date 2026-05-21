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
            if (SelectionFeedback is IUiSelectionFeedback explicitFeedback)
            {
                return explicitFeedback;
            }

            if (Button != null && Button.TryGetComponent<IUiSelectionFeedback>(out var buttonFeedback))
            {
                return buttonFeedback;
            }

            return SelectionFrame != null
                ? SelectionFrame.GetComponentInParent<IUiSelectionFeedback>(includeInactive: true)
                : null;
        }
    }
}
