using System;
using Game.Feature.UI.Application;
using Game.Shared.Display;

namespace Game.Feature.UI.Composition
{
    internal sealed class DisplaySettingsPortAdapter : IDisplaySettingsPort
    {
        private readonly IDisplaySettingsService displaySettingsService;

        public DisplaySettingsPortAdapter(IDisplaySettingsService displaySettingsService)
        {
            this.displaySettingsService = displaySettingsService ?? throw new ArgumentNullException(nameof(displaySettingsService));
        }

        public DisplaySettingsPortSnapshot Read()
        {
            var modes = displaySettingsService.ReadAvailableDisplayModes();
            var committed = displaySettingsService.ReadCommittedDisplaySettings();
            var current = displaySettingsService.ReadCurrentDisplaySettings();
            var visibleModes = new DisplaySettingsPortModeOption[modes.Count];
            var committedModeIndex = 0;

            for (var i = 0; i < modes.Count; i++)
            {
                visibleModes[i] = new DisplaySettingsPortModeOption(modes[i].Width, modes[i].Height, modes[i].VisibleLabel);
                if (modes[i].Width == committed.Width && modes[i].Height == committed.Height)
                {
                    committedModeIndex = i;
                }
            }

            return new DisplaySettingsPortSnapshot(
                visibleModes,
                committedModeIndex,
                UIDisplayWindowModeMapper.ToVisible(committed.WindowMode),
                current.VisibleResolutionLabel,
                UIDisplayWindowModeMapper.ToVisible(current.WindowMode),
                displaySettingsService.IsPreviewActive);
        }

        public bool BeginPreview(DisplaySettingsPortPreviewRequest request)
        {
            var modes = displaySettingsService.ReadAvailableDisplayModes();
            if (modes.Count == 0)
            {
                return false;
            }

            var clampedIndex = request.ModeIndex;
            if (clampedIndex < 0)
            {
                clampedIndex = 0;
            }
            else if (clampedIndex >= modes.Count)
            {
                clampedIndex = modes.Count - 1;
            }

            return displaySettingsService.BeginPreview(modes[clampedIndex].ToSnapshot(
                UIDisplayWindowModeMapper.ToShared(request.WindowMode)));
        }

        public bool CommitPreview()
        {
            return displaySettingsService.CommitPreview();
        }

        public bool RevertPreview()
        {
            return displaySettingsService.RevertPreview();
        }
    }
}
