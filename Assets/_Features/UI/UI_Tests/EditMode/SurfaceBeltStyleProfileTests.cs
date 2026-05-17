using Game.Feature.UI.HUD;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class SurfaceBeltStyleProfileTests
    {
        [Test]
        public void DefaultProfile_HasAllSlotStylesWithoutDuplicates()
        {
            var profile = ScriptableObject.CreateInstance<SurfaceBeltStyleProfile>();

            try
            {
                Assert.That(profile.TryValidate(out var message), Is.True, message);
                for (var slot = 0; slot < 4; slot++)
                {
                    Assert.That(profile.TryGetStyle(slot, out _), Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void TryValidate_DetectsDuplicateSlots()
        {
            var profile = ScriptableObject.CreateInstance<SurfaceBeltStyleProfile>();

            try
            {
                SetSlots(profile, 0, 1, 1, 3);

                Assert.That(profile.TryValidate(out var message), Is.False);
                Assert.That(message, Does.Contain("duplicated"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void TryValidate_DetectsMissingSlots()
        {
            var profile = ScriptableObject.CreateInstance<SurfaceBeltStyleProfile>();

            try
            {
                SetSlots(profile, 0, 1, 2);

                Assert.That(profile.TryValidate(out var message), Is.False);
                Assert.That(message, Does.Contain("missing"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        private static void SetSlots(SurfaceBeltStyleProfile profile, params int[] slots)
        {
            var serializedObject = new SerializedObject(profile);
            var styles = serializedObject.FindProperty("_slotStyles");
            styles.arraySize = slots.Length;
            for (var i = 0; i < slots.Length; i++)
            {
                var style = styles.GetArrayElementAtIndex(i);
                style.FindPropertyRelative("_slotIndex").intValue = slots[i];
                style.FindPropertyRelative("_color").colorValue = Color.white;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
