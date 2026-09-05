#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.UI.Popups;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class PauseProgressionActivationPlayModeTests
    {
        [UnityTest, Category("Full")]
        public IEnumerator InactiveParent_FirstMiddleLastSelectionIsVisibleAfterActivationAndReopen()
        {
            foreach (var index in new[] { 0, 5, 12 })
            {
                var parent = new GameObject("PopupLayer", typeof(RectTransform), typeof(Canvas));
                parent.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                parent.SetActive(false);
                try
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Features/UI/UI_Popups/Prefabs/PausePopup.prefab");
                    var root = Object.Instantiate(prefab, parent.transform, false);
                    var popup = root.GetComponent<PausePopupView>();
                    var strip = root.GetComponentInChildren<PauseProgressionStripView>(true);
                    var models = new List<PauseProgressionMarkerModel>();
                    for (var i = 0; i < 13; i++)
                        models.Add(new PauseProgressionMarkerModel($"test-stage-{i}"));
                    var model = new PauseProgressionViewModel(true, models, index);
                    // Test the strip lifecycle independently of the popup's view-model refresh.
                    popup.enabled = false;
                    root.SetActive(true);
                    strip.Bind(model);
                    parent.SetActive(true);
                    yield return null;
                    yield return null;

                    AssertSelectionVisible(strip, index);
                    var content = Field<RectTransform>(strip, "_content");
                    var scroll = Field<ScrollRect>(strip, "_scrollRect");
                    scroll.StopMovement();
                    content.anchoredPosition = new Vector2(-100f, content.anchoredPosition.y);
                    strip.Bind(model);
                    yield return null;
                    Assert.That(content.anchoredPosition.x, Is.EqualTo(-100f).Within(0.1f),
                        "Completed initial layout must not keep snapping manual scroll back.");

                    parent.SetActive(false);
                    parent.SetActive(true);
                    yield return null;
                    yield return null;
                    AssertSelectionVisible(strip, index);

                    strip.SelectIndex(index == 12 ? 11 : index + 1);
                    AssertSelectionVisible(strip, index == 12 ? 11 : index + 1);
                }
                finally
                {
                    Object.DestroyImmediate(parent);
                }
            }
        }

        private static void AssertSelectionVisible(PauseProgressionStripView strip, int index)
        {
            var markers = Field<List<PauseProgressionMarkerView>>(strip, "_markers");
            var viewport = Field<ScrollRect>(strip, "_scrollRect").viewport;
            Assert.That(strip.SelectedIndex, Is.EqualTo(index));
            Assert.That(markers[index].VisualImage.rectTransform.rect.size,
                Is.EqualTo(new Vector2(144f, 81f)));
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                viewport, markers[index].VisualImage.rectTransform);
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(viewport.rect.xMin - 0.1f));
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(viewport.rect.xMax + 0.1f));
        }

        private static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
}
#endif
