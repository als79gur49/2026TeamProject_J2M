using System;
using UnityEngine.UI;

namespace Game.Feature.UI.ViewShared
{
    [Serializable]
    public sealed class UiSelectableButtonSlot
    {
        public UiSelectableButtonSlot()
        {
        }

        public UiSelectableButtonSlot(Button button, Image selectionFrame)
        {
            Button = button;
            SelectionFrame = selectionFrame;
        }

        public Button Button;

        public Image SelectionFrame;

        public bool IsConfigured => Button != null && SelectionFrame != null;
    }
}
