using System;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Screens
{
    internal static class SettingsFocusGraphBinding
    {
        private const string HeaderBackNodeId = "Header.Back";
        private const string HeaderAudioTabNodeId = "Header.AudioTab";
        private const string HeaderDisplayTabNodeId = "Header.DisplayTab";
        private const string HeaderInputTabNodeId = "Header.InputTab";
        private const string AudioMainSliderNodeId = "Audio.Main.Slider";
        private const string AudioMainMuteNodeId = "Audio.Main.Mute";
        private const string AudioBgmSliderNodeId = "Audio.Bgm.Slider";
        private const string AudioBgmMuteNodeId = "Audio.Bgm.Mute";
        private const string AudioSfxSliderNodeId = "Audio.Sfx.Slider";
        private const string AudioSfxMuteNodeId = "Audio.Sfx.Mute";
        private const string DisplayResolutionDropdownNodeId = "Display.Resolution.Dropdown";
        private const string DisplayFullscreenToggleNodeId = "Display.Fullscreen.Toggle";
        private const string DisplayApplyButtonNodeId = "Display.Apply.Button";
        private const string DisplayRevertButtonNodeId = "Display.Revert.Button";
        private const string InputMovementToggleNodeId = "Input.Movement.Toggle";
        private const string InputPushChangeNodeId = "Input.Push.Change";
        private const string InputFlipChangeNodeId = "Input.Flip.Change";
        private const string InputResetNodeId = "Input.Reset";

        public static void Build(SettingsScreenView view, UiFocusGraphNavigator graph)
        {
            if (view == null || graph == null)
            {
                return;
            }

            view.RegisterFocusNode(
                HeaderBackNodeId,
                UiFocusRegion.Header,
                UiFocusNodeKind.Button,
                0,
                0,
                () => SettingsScreenView.InvokeAndReturnTrue(view.ClickBack));
            view.RegisterFocusNode(
                HeaderAudioTabNodeId,
                UiFocusRegion.Header,
                UiFocusNodeKind.Button,
                0,
                1,
                () => view.SelectSectionFromKeyboard(SettingsSectionId.Audio),
                isInteractable: () => view.CurrentSectionId() != SettingsSectionId.Audio);
            view.RegisterFocusNode(
                HeaderDisplayTabNodeId,
                UiFocusRegion.Header,
                UiFocusNodeKind.Button,
                0,
                2,
                () => view.SelectSectionFromKeyboard(SettingsSectionId.Display),
                isInteractable: () => view.CurrentSectionId() != SettingsSectionId.Display);
            view.RegisterFocusNode(
                HeaderInputTabNodeId,
                UiFocusRegion.Header,
                UiFocusNodeKind.Button,
                0,
                3,
                () => view.SelectSectionFromKeyboard(SettingsSectionId.Input),
                isInteractable: () => view.CurrentSectionId() != SettingsSectionId.Input);

            AddAudioNodes(view, AudioSettingsChannel.Main, AudioMainSliderNodeId, AudioMainMuteNodeId, 0);
            AddAudioNodes(view, AudioSettingsChannel.Bgm, AudioBgmSliderNodeId, AudioBgmMuteNodeId, 1);
            AddAudioNodes(view, AudioSettingsChannel.Sfx, AudioSfxSliderNodeId, AudioSfxMuteNodeId, 2);

            view.RegisterFocusNode(
                DisplayResolutionDropdownNodeId,
                UiFocusRegion.Display,
                UiFocusNodeKind.Dropdown,
                0,
                0,
                submit: null,
                adjust: view.AdjustResolution);
            view.RegisterFocusNode(
                DisplayFullscreenToggleNodeId,
                UiFocusRegion.Display,
                UiFocusNodeKind.Toggle,
                1,
                0,
                () => SettingsScreenView.InvokeAndReturnTrue(() => view.SetDisplayFullscreen(!view.IsDisplayFullscreenOn)));
            view.RegisterFocusNode(
                DisplayApplyButtonNodeId,
                UiFocusRegion.Display,
                UiFocusNodeKind.Button,
                2,
                0,
                () => SettingsScreenView.InvokeAndReturnTrue(view.ClickDisplayApply),
                isInteractable: () => view.IsDisplayApplyInteractable);
            view.RegisterFocusNode(
                DisplayRevertButtonNodeId,
                UiFocusRegion.Display,
                UiFocusNodeKind.Button,
                2,
                1,
                () => SettingsScreenView.InvokeAndReturnTrue(view.ClickDisplayRevert),
                isInteractable: () => view.IsDisplayRevertInteractable);

            view.RegisterFocusNode(
                InputMovementToggleNodeId,
                UiFocusRegion.Input,
                UiFocusNodeKind.Toggle,
                0,
                0,
                () => view.InputView != null &&
                      SettingsScreenView.InvokeAndReturnTrue(() => view.InputView.SetMovementUseArrowKeys(!view.InputView.IsMovementToggleOn)));
            view.RegisterFocusNode(
                InputPushChangeNodeId,
                UiFocusRegion.Input,
                UiFocusNodeKind.Button,
                1,
                0,
                () => SettingsScreenView.InvokeAndReturnTrue(view.InputView.ClickPushChange),
                isInteractable: () => view.InputView != null && view.InputView.IsPushChangeInteractable);
            view.RegisterFocusNode(
                InputFlipChangeNodeId,
                UiFocusRegion.Input,
                UiFocusNodeKind.Button,
                2,
                0,
                () => SettingsScreenView.InvokeAndReturnTrue(view.InputView.ClickFlipChange),
                isInteractable: () => view.InputView != null && view.InputView.IsFlipChangeInteractable);
            view.RegisterFocusNode(
                InputResetNodeId,
                UiFocusRegion.Input,
                UiFocusNodeKind.Button,
                3,
                0,
                () => SettingsScreenView.InvokeAndReturnTrue(view.InputView.ClickReset),
                isInteractable: () => view.InputView != null && view.InputView.IsResetInteractable);
        }

        public static void CommitEditedNode(SettingsScreenView view, string nodeId)
        {
            if (view == null)
            {
                return;
            }

            if (string.Equals(nodeId, AudioMainSliderNodeId, StringComparison.Ordinal))
            {
                view.CommitAudioInteraction(AudioSettingsChannel.Main);
            }
            else if (string.Equals(nodeId, AudioBgmSliderNodeId, StringComparison.Ordinal))
            {
                view.CommitAudioInteraction(AudioSettingsChannel.Bgm);
            }
            else if (string.Equals(nodeId, AudioSfxSliderNodeId, StringComparison.Ordinal))
            {
                view.CommitAudioInteraction(AudioSettingsChannel.Sfx);
            }
        }

        private static void AddAudioNodes(
            SettingsScreenView view,
            AudioSettingsChannel channel,
            string sliderNodeId,
            string muteNodeId,
            int row)
        {
            view.RegisterFocusNode(
                sliderNodeId,
                UiFocusRegion.Audio,
                UiFocusNodeKind.Slider,
                row,
                0,
                submit: null,
                adjust: delta => view.AdjustAudioVolume(channel, delta));
            view.RegisterFocusNode(
                muteNodeId,
                UiFocusRegion.Audio,
                UiFocusNodeKind.Toggle,
                row,
                1,
                () => view.AudioView != null &&
                      SettingsScreenView.InvokeAndReturnTrue(() => view.SetAudioMuted(channel, !view.AudioView.IsMuted(channel))));
        }

        private static SettingsSectionId CurrentSectionId(this SettingsScreenView view)
        {
            return view != null && view.CurrentSectionIdForNavigation.HasValue
                ? view.CurrentSectionIdForNavigation.Value
                : SettingsSectionId.Audio;
        }
    }
}
