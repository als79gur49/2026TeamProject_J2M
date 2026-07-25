using Game.Feature.UI.Screens;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Feature.UI.Tests
{
    public sealed class SettingsAudioViewListenerContractTests
    {
        [Test]
        public void SettingsAudioView_RefreshView_PreservesExternalSliderListeners()
        {
            var settingsView = InstantiateSettingsScreenView();
            try
            {
                var audioView = settingsView.AudioView;
                var viewModel = CreateViewModel(1f, false, 1f, false, 1f, false);
                audioView.Bind(viewModel);
                audioView.SetIsVisible(true);

                var slider = FindRowSlider(audioView, "MainAudioRow");
                var externalCallCount = 0;
                var lastValue = -1f;
                slider.onValueChanged.AddListener(value =>
                {
                    externalCallCount++;
                    lastValue = value;
                });

                viewModel.SetContent(
                    CreateRow(0.8f, false),
                    CreateRow(0.6f, false),
                    CreateRow(0.4f, false));
                audioView.SetVolume(AudioSettingsChannel.Main, 0.25f);

                Assert.That(externalCallCount, Is.EqualTo(1));
                Assert.That(lastValue, Is.EqualTo(0.25f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(settingsView.gameObject);
            }
        }

        [Test]
        public void SettingsAudioView_DisableEnable_PreservesExternalToggleListeners()
        {
            var settingsView = InstantiateSettingsScreenView();
            try
            {
                var audioView = settingsView.AudioView;
                var viewModel = CreateViewModel(1f, false, 1f, false, 1f, false);
                audioView.Bind(viewModel);
                audioView.SetIsVisible(true);

                var toggle = FindRowToggle(audioView, "BgmAudioRow");
                var externalCallCount = 0;
                var lastIsMuted = false;
                toggle.onValueChanged.AddListener(isMuted =>
                {
                    externalCallCount++;
                    lastIsMuted = isMuted;
                });

                audioView.gameObject.SetActive(false);
                audioView.gameObject.SetActive(true);
                audioView.SetIsVisible(true);
                audioView.SetMuted(AudioSettingsChannel.Bgm, true);

                Assert.That(externalCallCount, Is.EqualTo(1));
                Assert.That(lastIsMuted, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(settingsView.gameObject);
            }
        }

        [Test]
        public void SettingsAudioView_RepeatedRefresh_DoesNotDuplicateSemanticVolumeEvents()
        {
            var settingsView = InstantiateSettingsScreenView();
            try
            {
                var audioView = settingsView.AudioView;
                var viewModel = CreateViewModel(1f, false, 1f, false, 1f, false);
                audioView.Bind(viewModel);
                audioView.SetIsVisible(true);

                var volumeChangedCallCount = 0;
                var lastChannel = AudioSettingsChannel.Sfx;
                var lastValue = -1f;
                audioView.VolumeChanged += (channel, value) =>
                {
                    volumeChangedCallCount++;
                    lastChannel = channel;
                    lastValue = value;
                };

                viewModel.SetContent(
                    CreateRow(0.9f, false),
                    CreateRow(0.7f, false),
                    CreateRow(0.5f, false));
                viewModel.SetContent(
                    CreateRow(0.85f, false),
                    CreateRow(0.65f, false),
                    CreateRow(0.45f, false));
                audioView.SetVolume(AudioSettingsChannel.Main, 0.33f);

                Assert.That(volumeChangedCallCount, Is.EqualTo(1));
                Assert.That(lastChannel, Is.EqualTo(AudioSettingsChannel.Main));
                Assert.That(lastValue, Is.EqualTo(0.33f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(settingsView.gameObject);
            }
        }

        [Test]
        public void SettingsAudioView_CommitInteractionWithoutValueChange_DoesNotCompleteInteraction()
        {
            var settingsView = InstantiateSettingsScreenView();
            try
            {
                var audioView = settingsView.AudioView;
                var viewModel = CreateViewModel(0.5f, false, 0.5f, false, 0.5f, false);
                audioView.Bind(viewModel);
                audioView.SetIsVisible(true);
                var completedCount = 0;
                audioView.InteractionCompleted += () => completedCount++;

                audioView.BeginInteraction(AudioSettingsChannel.Main);
                audioView.CommitInteraction(AudioSettingsChannel.Main);

                Assert.That(completedCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(settingsView.gameObject);
            }
        }

        [Test]
        public void SettingsAudioView_CommitInteractionAfterValueChange_CompletesInteraction()
        {
            var settingsView = InstantiateSettingsScreenView();
            try
            {
                var audioView = settingsView.AudioView;
                var viewModel = CreateViewModel(0.5f, false, 0.5f, false, 0.5f, false);
                audioView.Bind(viewModel);
                audioView.SetIsVisible(true);
                var completedCount = 0;
                audioView.InteractionCompleted += () => completedCount++;

                audioView.BeginInteraction(AudioSettingsChannel.Main);
                audioView.SetVolume(AudioSettingsChannel.Main, 0.75f);
                audioView.CommitInteraction(AudioSettingsChannel.Main);

                Assert.That(completedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(settingsView.gameObject);
            }
        }

        [Test]
        public void SettingsAudioView_MuteLabels_FitAuthoredToggleLabelWidth()
        {
            var settingsView = InstantiateSettingsScreenView();
            try
            {
                settingsView.AudioView.gameObject.SetActive(true);
                Canvas.ForceUpdateCanvases();

                AssertMuteLabelFits(settingsView.AudioView, "MainAudioRow");
                AssertMuteLabelFits(settingsView.AudioView, "BgmAudioRow");
                AssertMuteLabelFits(settingsView.AudioView, "SfxAudioRow");
            }
            finally
            {
                Object.DestroyImmediate(settingsView.gameObject);
            }
        }

        private static SettingsScreenView InstantiateSettingsScreenView()
        {
            return Object.Instantiate(
                UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(
                    UiTestPrefabAssetUtility.SettingsScreenPrefabPath));
        }

        private static SettingsAudioViewModel CreateViewModel(
            float mainVolume,
            bool mainMuted,
            float bgmVolume,
            bool bgmMuted,
            float sfxVolume,
            bool sfxMuted)
        {
            var viewModel = new SettingsAudioViewModel();
            viewModel.SetContent(
                CreateRow(mainVolume, mainMuted),
                CreateRow(bgmVolume, bgmMuted),
                CreateRow(sfxVolume, sfxMuted));
            return viewModel;
        }

        private static AudioSettingsRowViewModel CreateRow(float normalizedValue, bool isMuted)
        {
            var percent = Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * 100f);
            var valueText = isMuted
                ? $"{percent}% (Muted)"
                : $"{percent}%";
            return new AudioSettingsRowViewModel(valueText, normalizedValue, isMuted);
        }

        private static Slider FindRowSlider(SettingsAudioView audioView, string rowName)
        {
            var row = audioView.transform.Find(rowName);
            Assert.That(row, Is.Not.Null, rowName);

            var slider = row.GetComponentInChildren<Slider>(includeInactive: true);
            Assert.That(slider, Is.Not.Null, $"{rowName} slider");
            return slider;
        }

        private static Toggle FindRowToggle(SettingsAudioView audioView, string rowName)
        {
            var row = audioView.transform.Find(rowName);
            Assert.That(row, Is.Not.Null, rowName);

            var toggle = row.GetComponentInChildren<Toggle>(includeInactive: true);
            Assert.That(toggle, Is.Not.Null, $"{rowName} toggle");
            return toggle;
        }

        private static void AssertMuteLabelFits(SettingsAudioView audioView, string rowName)
        {
            var label = FindMuteLabel(audioView, rowName);
            var rectTransform = label.rectTransform;
            var preferredWidth = label.GetPreferredValues(label.text, Mathf.Infinity, rectTransform.rect.height).x;

            Assert.That(label.text, Is.EqualTo("Mute"), rowName);
            Assert.That(rectTransform.rect.width, Is.GreaterThanOrEqualTo(60f), rowName);
            Assert.That(rectTransform.rect.width, Is.GreaterThanOrEqualTo(preferredWidth), rowName);
        }

        private static TMP_Text FindMuteLabel(SettingsAudioView audioView, string rowName)
        {
            var row = audioView.transform.Find(rowName);
            Assert.That(row, Is.Not.Null, rowName);

            var label = row.Find("MuteToggle/Label")?.GetComponent<TMP_Text>();
            Assert.That(label, Is.Not.Null, $"{rowName} mute label");
            return label;
        }
    }
}
