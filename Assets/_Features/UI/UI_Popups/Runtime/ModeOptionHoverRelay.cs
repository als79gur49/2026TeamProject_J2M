using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Popups
{
    [DisallowMultipleComponent]
    public sealed class ModeOptionHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private int _optionIndex;

        public event Action<int> HoverEntered;
        public event Action<int> HoverExited;

        public void OnPointerEnter(PointerEventData eventData)
        {
            HoverEntered?.Invoke(_optionIndex);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverExited?.Invoke(_optionIndex);
        }
    }
}
