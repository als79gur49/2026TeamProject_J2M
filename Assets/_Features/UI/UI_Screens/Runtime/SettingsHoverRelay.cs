using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Screens
{
    [DisallowMultipleComponent]
    public sealed class SettingsHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action HoverEntered;

        public event Action HoverExited;

        public void OnPointerEnter(PointerEventData eventData)
        {
            HoverEntered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverExited?.Invoke();
        }
    }
}
