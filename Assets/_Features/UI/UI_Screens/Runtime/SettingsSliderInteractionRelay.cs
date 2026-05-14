using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Screens
{
    [DisallowMultipleComponent]
    public sealed class SettingsSliderInteractionRelay : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IPointerUpHandler
    {
        private bool _hasActiveGesture;
        private bool _hasEmittedCommitThisGesture;

        public event Action InteractionStarted;

        public event Action InteractionCompleted;

        public void OnPointerDown(PointerEventData eventData)
        {
            BeginGesture();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            BeginGesture();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            EmitGestureCommit();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EmitGestureCommit();
            ResetGestureState();
        }

        public void RaiseInteractionCompleted()
        {
            InteractionCompleted?.Invoke();
        }

        private void OnDisable()
        {
            ResetGestureState();
        }

        private void BeginGesture()
        {
            if (_hasActiveGesture)
            {
                return;
            }

            _hasActiveGesture = true;
            _hasEmittedCommitThisGesture = false;
            InteractionStarted?.Invoke();
        }

        private void EmitGestureCommit()
        {
            if (_hasEmittedCommitThisGesture)
            {
                return;
            }

            if (!_hasActiveGesture)
            {
                _hasActiveGesture = true;
            }

            _hasEmittedCommitThisGesture = true;
            InteractionCompleted?.Invoke();
        }

        private void ResetGestureState()
        {
            _hasActiveGesture = false;
            _hasEmittedCommitThisGesture = false;
        }
    }
}
