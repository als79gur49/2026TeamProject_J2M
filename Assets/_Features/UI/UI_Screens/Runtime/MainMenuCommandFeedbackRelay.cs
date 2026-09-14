using Game.Feature.UI.ViewShared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class MainMenuCommandFeedbackRelay : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IUiSelectionFeedback
    {
        [SerializeField] private MainMenuScreenView _owner;
        [SerializeField] private MainMenuCommandId _commandId;
        [SerializeField] private UiHoverScaleEffect _selectionFeedback;

        private Selectable _selectable;
        private bool _pointerInside;
        private bool _navigationFocused;

        public void ValidateAuthoredStructureOrThrow()
        {
            if (_owner == null || _commandId == MainMenuCommandId.None || _selectionFeedback == null)
            {
                throw new System.InvalidOperationException(
                    $"{nameof(MainMenuCommandFeedbackRelay)} on '{name}' is missing its owner, command ID, or UiHoverScaleEffect delegate.");
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            if (IsInteractionAllowed())
            {
                _owner?.NotifyCommandPointerFocus(_commandId, focused: true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            _owner?.NotifyCommandPointerFocus(_commandId, focused: false);
        }

        public void SetNavigationFocused(bool focused)
        {
            _navigationFocused = focused;
            _selectionFeedback?.SetNavigationFocused(focused);
            _owner?.NotifyCommandNavigationFocus(
                _commandId,
                focused && IsInteractionAllowed());
        }

        public void PlaySubmitFeedback()
        {
            _selectionFeedback?.PlaySubmitFeedback();
        }

        private void OnDisable()
        {
            if (_pointerInside)
            {
                _owner?.NotifyCommandPointerFocus(_commandId, focused: false);
            }

            if (_navigationFocused)
            {
                _owner?.NotifyCommandNavigationFocus(_commandId, focused: false);
            }

            _pointerInside = false;
            _navigationFocused = false;
        }

        private bool IsInteractionAllowed()
        {
            _selectable ??= GetComponent<Selectable>();
            return _selectable != null && _selectable.IsActive() && _selectable.IsInteractable();
        }
    }
}
