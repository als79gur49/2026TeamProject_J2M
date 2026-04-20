using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Screens
{
    [DisallowMultipleComponent]
    public sealed class SettingsSliderInteractionRelay : MonoBehaviour, IEndDragHandler, IPointerUpHandler
    {
        public event Action InteractionCompleted;

        public void OnEndDrag(PointerEventData eventData)
        {
            InteractionCompleted?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            InteractionCompleted?.Invoke();
        }

        public void RaiseInteractionCompleted()
        {
            InteractionCompleted?.Invoke();
        }
    }
}
