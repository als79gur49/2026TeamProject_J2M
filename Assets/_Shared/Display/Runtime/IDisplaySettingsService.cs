using System.Collections.Generic;

namespace Game.Shared.Display
{
    public interface IDisplaySettingsService
    {
        bool IsPreviewActive { get; }

        bool HasBootApplied { get; }

        DisplaySettingsSnapshot ReadCurrentDisplaySettings();

        DisplaySettingsSnapshot ReadCommittedDisplaySettings();

        IReadOnlyList<DisplayModeOption> ReadAvailableDisplayModes();

        bool ApplyBootSettingsOnce();

        bool BeginPreview(DisplaySettingsSnapshot snapshot);

        bool CommitPreview();

        bool RevertPreview();
    }
}
