using Game.Feature.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Tests
{
    public sealed class SettingsSliderInteractionRelayTests
    {
        [Test]
        public void SettingsSliderInteractionRelay_DragEndFollowedByPointerUp_EmitsExactlyOnce()
        {
            var rootObject = new GameObject("SettingsSliderInteractionRelay_DragEndFollowedByPointerUp_EmitsExactlyOnce");

            try
            {
                var relay = rootObject.AddComponent<SettingsSliderInteractionRelay>();
                var completedCount = 0;
                relay.InteractionCompleted += () => completedCount++;
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                var eventData = new PointerEventData(eventSystemObject.GetComponent<EventSystem>());

                relay.OnPointerDown(eventData);
                relay.OnBeginDrag(eventData);
                relay.OnEndDrag(eventData);
                relay.OnPointerUp(eventData);

                Assert.That(completedCount, Is.EqualTo(1));
                Object.DestroyImmediate(eventSystemObject);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void SettingsSliderInteractionRelay_PointerUpWithoutDrag_EmitsExactlyOnce()
        {
            var rootObject = new GameObject("SettingsSliderInteractionRelay_PointerUpWithoutDrag_EmitsExactlyOnce");

            try
            {
                var relay = rootObject.AddComponent<SettingsSliderInteractionRelay>();
                var completedCount = 0;
                relay.InteractionCompleted += () => completedCount++;
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                var eventData = new PointerEventData(eventSystemObject.GetComponent<EventSystem>());

                relay.OnPointerDown(eventData);
                relay.OnPointerUp(eventData);

                Assert.That(completedCount, Is.EqualTo(1));
                Object.DestroyImmediate(eventSystemObject);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void SettingsSliderInteractionRelay_RepeatedGestures_EachEmitOneCommit()
        {
            var rootObject = new GameObject("SettingsSliderInteractionRelay_RepeatedGestures_EachEmitOneCommit");

            try
            {
                var relay = rootObject.AddComponent<SettingsSliderInteractionRelay>();
                var completedCount = 0;
                relay.InteractionCompleted += () => completedCount++;
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                var eventData = new PointerEventData(eventSystemObject.GetComponent<EventSystem>());

                relay.OnPointerDown(eventData);
                relay.OnPointerUp(eventData);
                relay.OnPointerDown(eventData);
                relay.OnBeginDrag(eventData);
                relay.OnEndDrag(eventData);
                relay.OnPointerUp(eventData);

                Assert.That(completedCount, Is.EqualTo(2));
                Object.DestroyImmediate(eventSystemObject);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void SettingsSliderInteractionRelay_DisableAndReenable_ClearsStaleGestureState()
        {
            var rootObject = new GameObject("SettingsSliderInteractionRelay_DisableAndReenable_ClearsStaleGestureState");

            try
            {
                var relay = rootObject.AddComponent<SettingsSliderInteractionRelay>();
                var completedCount = 0;
                relay.InteractionCompleted += () => completedCount++;
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                var eventData = new PointerEventData(eventSystemObject.GetComponent<EventSystem>());

                relay.OnPointerDown(eventData);
                relay.enabled = false;
                relay.enabled = true;
                relay.OnPointerDown(eventData);
                relay.OnPointerUp(eventData);

                Assert.That(completedCount, Is.EqualTo(1));
                Object.DestroyImmediate(eventSystemObject);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }
    }
}
