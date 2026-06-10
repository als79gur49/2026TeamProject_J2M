using System;
using System.Collections.Generic;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public readonly struct AudioSettingsPortChannelState
    {
        public AudioSettingsPortChannelState(float volume, bool isMuted)
        {
            Volume = volume;
            IsMuted = isMuted;
        }

        public float Volume { get; }

        public bool IsMuted { get; }
    }

    public readonly struct AudioSettingsPortSnapshot
    {
        public AudioSettingsPortSnapshot(
            AudioSettingsPortChannelState main,
            AudioSettingsPortChannelState bgm,
            AudioSettingsPortChannelState sfx)
        {
            Main = main;
            Bgm = bgm;
            Sfx = sfx;
        }

        public AudioSettingsPortChannelState Main { get; }

        public AudioSettingsPortChannelState Bgm { get; }

        public AudioSettingsPortChannelState Sfx { get; }

        public AudioSettingsPortChannelState GetChannelState(AudioSettingsChannel channel)
        {
            switch (channel)
            {
                case AudioSettingsChannel.Main:
                    return Main;
                case AudioSettingsChannel.Bgm:
                    return Bgm;
                case AudioSettingsChannel.Sfx:
                    return Sfx;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }
    }

    public interface IAudioSettingsPort
    {
        AudioSettingsPortSnapshot Read();

        void SetVolume(AudioSettingsChannel channel, float volume);

        void SetMuted(AudioSettingsChannel channel, bool isMuted);

        void Flush();
    }

    public enum DisplayWindowMode
    {
        Windowed = 0,
        FullScreenWindow = 1,
    }

    public readonly struct DisplaySettingsPortModeOption
    {
        public DisplaySettingsPortModeOption(int width, int height, string labelText)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            LabelText = labelText ?? string.Empty;
        }

        public int Width { get; }

        public int Height { get; }

        public string LabelText { get; }
    }

    public readonly struct DisplaySettingsPortPreviewRequest
    {
        public DisplaySettingsPortPreviewRequest(int modeIndex, DisplayWindowMode windowMode)
        {
            ModeIndex = modeIndex;
            WindowMode = windowMode;
        }

        public int ModeIndex { get; }

        public DisplayWindowMode WindowMode { get; }
    }

    public readonly struct DisplaySettingsPortSnapshot
    {
        public DisplaySettingsPortSnapshot(
            IReadOnlyList<DisplaySettingsPortModeOption> availableModes,
            int committedModeIndex,
            DisplayWindowMode committedWindowMode,
            string currentRuntimeResolutionLabel,
            DisplayWindowMode currentRuntimeWindowMode,
            bool isPreviewActive)
        {
            AvailableModes = availableModes ?? Array.Empty<DisplaySettingsPortModeOption>();
            CommittedModeIndex = committedModeIndex;
            CommittedWindowMode = committedWindowMode;
            CurrentRuntimeResolutionLabel = currentRuntimeResolutionLabel ?? string.Empty;
            CurrentRuntimeWindowMode = currentRuntimeWindowMode;
            IsPreviewActive = isPreviewActive;
        }

        public IReadOnlyList<DisplaySettingsPortModeOption> AvailableModes { get; }

        public int CommittedModeIndex { get; }

        public DisplayWindowMode CommittedWindowMode { get; }

        public string CurrentRuntimeResolutionLabel { get; }

        public DisplayWindowMode CurrentRuntimeWindowMode { get; }

        public bool IsPreviewActive { get; }
    }

    public interface IDisplaySettingsPort
    {
        DisplaySettingsPortSnapshot Read();

        bool BeginPreview(DisplaySettingsPortPreviewRequest request);

        bool CommitPreview();

        bool RevertPreview();
    }
}
