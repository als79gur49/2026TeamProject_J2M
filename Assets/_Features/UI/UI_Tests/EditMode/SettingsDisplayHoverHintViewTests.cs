using System.Reflection;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class SettingsDisplayHoverHintViewTests
    {
        [Test]
        public void SettingsDisplayView_ResolutionHoverHint_ShowsOnEnter_AndHidesOnExit()
        {
            var settingsScreen = Object.Instantiate(
                UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath));

            try
            {
                var displayView = settingsScreen.DisplayView;
                var viewModel = CreateDisplayViewModel();
                displayView.Bind(viewModel);
                settingsScreen.SetIsCurrent(true);

                var relay = GetPrivateField<SettingsHoverRelay>(displayView, "_resolutionHoverRelay");
                var hintRoot = GetPrivateField<RectTransform>(displayView, "_resolutionHoverHintRoot");
                var hintLabel = GetPrivateField<TMP_Text>(displayView, "_resolutionHoverHintLabel");

                Assert.That(hintRoot.gameObject.activeSelf, Is.False);

                relay.OnPointerEnter(null);

                Assert.That(hintRoot.gameObject.activeSelf, Is.True);
                Assert.That(hintLabel.text, Is.EqualTo("Only automatically detected resolutions are shown."));

                relay.OnPointerExit(null);

                Assert.That(hintRoot.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(settingsScreen.gameObject);
            }
        }

        [Test]
        public void SettingsDisplayView_ResolutionHoverHint_HidesOnDisable()
        {
            var settingsScreen = Object.Instantiate(
                UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath));

            try
            {
                var displayView = settingsScreen.DisplayView;
                displayView.Bind(CreateDisplayViewModel());
                settingsScreen.SetIsCurrent(true);

                var relay = GetPrivateField<SettingsHoverRelay>(displayView, "_resolutionHoverRelay");
                var hintRoot = GetPrivateField<RectTransform>(displayView, "_resolutionHoverHintRoot");

                relay.OnPointerEnter(null);
                Assert.That(hintRoot.gameObject.activeSelf, Is.True);

                settingsScreen.gameObject.SetActive(false);
                Assert.That(hintRoot.gameObject.activeInHierarchy, Is.False);

            }
            finally
            {
                Object.DestroyImmediate(settingsScreen.gameObject);
            }
        }

        [Test]
        public void SettingsDisplayView_ResolutionHoverHint_HidesWhenViewBecomesInvisible_AndRepeatedHideIsSafe()
        {
            var settingsScreen = Object.Instantiate(
                UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath));

            try
            {
                var displayView = settingsScreen.DisplayView;
                displayView.Bind(CreateDisplayViewModel());
                settingsScreen.SetIsCurrent(true);

                var relay = GetPrivateField<SettingsHoverRelay>(displayView, "_resolutionHoverRelay");
                var hintRoot = GetPrivateField<RectTransform>(displayView, "_resolutionHoverHintRoot");

                relay.OnPointerEnter(null);
                Assert.That(hintRoot.gameObject.activeSelf, Is.True);

                Assert.DoesNotThrow(() =>
                {
                    displayView.SetIsVisible(false);
                    displayView.SetIsVisible(false);
                });

                Assert.That(hintRoot.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(settingsScreen.gameObject);
            }
        }

        [Test]
        public void SettingsDisplayView_ResolutionHoverHint_HidesOnViewModelRefresh_AndBindReplacesLocalHoverState()
        {
            var settingsScreen = Object.Instantiate(
                UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath));

            try
            {
                var displayView = settingsScreen.DisplayView;
                var viewModel = CreateDisplayViewModel();
                displayView.Bind(viewModel);
                settingsScreen.SetIsCurrent(true);

                var relay = GetPrivateField<SettingsHoverRelay>(displayView, "_resolutionHoverRelay");
                var hintRoot = GetPrivateField<RectTransform>(displayView, "_resolutionHoverHintRoot");
                var hintLabel = GetPrivateField<TMP_Text>(displayView, "_resolutionHoverHintLabel");

                relay.OnPointerEnter(null);
                Assert.That(hintRoot.gameObject.activeSelf, Is.True);

                viewModel.SetContent(
                    "Display",
                    "Current Display",
                    "1920 x 1080",
                    "Resolution",
                    "Updated hover hint.",
                    new[] { "1920 x 1080", "1600 x 900" },
                    0,
                    "Fullscreen Window",
                    false,
                    string.Empty,
                    "Apply",
                    false,
                    "Revert",
                    false,
                    false,
                    string.Empty,
                    0f,
                    false);

                Assert.That(hintRoot.gameObject.activeSelf, Is.False);
                Assert.That(hintLabel.text, Is.EqualTo("Updated hover hint."));

                relay.OnPointerEnter(null);
                Assert.That(hintRoot.gameObject.activeSelf, Is.True);

                displayView.Bind(CreateDisplayViewModel());

                Assert.That(hintRoot.gameObject.activeSelf, Is.False);
                Assert.That(hintLabel.text, Is.EqualTo("Only automatically detected resolutions are shown."));
            }
            finally
            {
                Object.DestroyImmediate(settingsScreen.gameObject);
            }
        }

        private static SettingsDisplayViewModel CreateDisplayViewModel()
        {
            var viewModel = new SettingsDisplayViewModel();
            viewModel.SetContent(
                "Display",
                "Current Display",
                "1920 x 1080",
                "Resolution",
                "Only automatically detected resolutions are shown.",
                new[] { "1920 x 1080", "1600 x 900" },
                0,
                "Fullscreen Window",
                false,
                string.Empty,
                "Apply",
                false,
                "Revert",
                false,
                false,
                string.Empty,
                0f,
                false);
            return viewModel;
        }

        private static TField GetPrivateField<TField>(object target, string fieldName)
            where TField : class
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            var value = field.GetValue(target) as TField;
            Assert.That(value, Is.Not.Null, fieldName);
            return value;
        }
    }
}
