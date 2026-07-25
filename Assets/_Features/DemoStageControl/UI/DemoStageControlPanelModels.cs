using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.DemoStageControl.UI
{
    public sealed class DemoStageControlPanelPayload : IPopupPayload
    {
        public DemoStageControlPanelPayload(
            IReadOnlyList<DemoStageControlStageItem> stages,
            DemoStageControlStatus status,
            DemoGameplayOverrideStatus overrideStatus = default)
        {
            Stages = stages ?? Array.Empty<DemoStageControlStageItem>();
            Status = status;
            OverrideStatus = overrideStatus;
        }

        public IReadOnlyList<DemoStageControlStageItem> Stages { get; }

        public DemoStageControlStatus Status { get; }

        public DemoGameplayOverrideStatus OverrideStatus { get; }
    }

    public sealed class DemoStageControlPanelViewModel
    {
        private readonly ILocalizedTextResolver _localizedTextResolver;

        public DemoStageControlPanelViewModel(ILocalizedTextResolver localizedTextResolver)
        {
            _localizedTextResolver = localizedTextResolver
                ?? throw new ArgumentNullException(nameof(localizedTextResolver));
        }

        public event Action Changed;

        public IReadOnlyList<DemoStageControlStageItem> Stages { get; private set; } =
            Array.Empty<DemoStageControlStageItem>();

        public StageId SelectedStageId { get; private set; } = StageId.None;

        public int SelectedStageIndex { get; private set; }

        public string CurrentStageText { get; private set; } = "Current StageId: none";

        public string CampaignActiveStageText { get; private set; } = "Campaign Active StageId: none";

        public string SelectedStageText { get; private set; } = "No stages";

        public string LastResultText { get; private set; } = string.Empty;

        public bool PlayerInvincible { get; private set; }

        public string PlayerInvincibleText { get; private set; } = "Player Invincible: OFF";

        public bool CanStartSelectedStage { get; private set; }

        public bool CanForceClearCurrentStage { get; private set; }

        public void Apply(DemoStageControlPanelPayload payload, StageId preferredSelection)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            Stages = payload.Stages ?? Array.Empty<DemoStageControlStageItem>();
            SelectedStageIndex = ResolveSelectedIndex(Stages, preferredSelection, payload.Status.CurrentStageId);
            SelectedStageId = Stages.Count > 0 ? Stages[SelectedStageIndex].StageId : StageId.None;
            CurrentStageText = FormatStageLine("Current StageId", payload.Status.CurrentStageId);
            CampaignActiveStageText = FormatStageLine("Campaign Active StageId", payload.Status.CampaignActiveStageId);
            SelectedStageText = FormatSelectedStage(Stages, SelectedStageIndex);
            LastResultText = string.IsNullOrWhiteSpace(payload.Status.LastResultMessage)
                ? payload.OverrideStatus.LastOverrideMessage
                : payload.Status.LastResultMessage;
            PlayerInvincible = payload.OverrideStatus.PlayerInvincible;
            PlayerInvincibleText = PlayerInvincible
                ? "Player Invincible: ON"
                : "Player Invincible: OFF";
            CanStartSelectedStage = SelectedStageId.IsValid && !payload.Status.IsSceneTransitionInProgress;
            CanForceClearCurrentStage = !payload.Status.IsCompletionInProgress;
            Changed?.Invoke();
        }

        public void SelectIndex(int index)
        {
            if (Stages.Count == 0)
            {
                SelectedStageIndex = 0;
                SelectedStageId = StageId.None;
                SelectedStageText = "No stages";
                Changed?.Invoke();
                return;
            }

            SelectedStageIndex = Math.Max(0, Math.Min(index, Stages.Count - 1));
            SelectedStageId = Stages[SelectedStageIndex].StageId;
            SelectedStageText = FormatSelectedStage(Stages, SelectedStageIndex);
            Changed?.Invoke();
        }

        public void RefreshLocale()
        {
            SelectedStageText = FormatSelectedStage(Stages, SelectedStageIndex);
            Changed?.Invoke();
        }

        private static int ResolveSelectedIndex(
            IReadOnlyList<DemoStageControlStageItem> stages,
            StageId preferredSelection,
            StageId currentStageId)
        {
            if (stages == null || stages.Count == 0)
            {
                return 0;
            }

            var target = preferredSelection.IsValid ? preferredSelection : currentStageId;
            if (target.IsValid)
            {
                for (var i = 0; i < stages.Count; i++)
                {
                    if (stages[i].StageId.Equals(target))
                    {
                        return i;
                    }
                }
            }

            return 0;
        }

        private static string FormatStageLine(string label, StageId stageId)
        {
            return stageId.IsValid ? $"{label}: {stageId.Value}" : $"{label}: none";
        }

        private string FormatSelectedStage(IReadOnlyList<DemoStageControlStageItem> stages, int index)
        {
            if (stages == null || stages.Count == 0)
            {
                return "No stages";
            }

            var item = stages[Math.Max(0, Math.Min(index, stages.Count - 1))];
            var state = item.IsCurrent ? "current" : item.IsUnlocked ? "unlocked" : "locked";
            var displayName = ResolveDisplayName(item);
            return $"{index + 1}/{stages.Count}  {displayName} ({item.StageId.Value}, {state})";
        }

        private string ResolveDisplayName(DemoStageControlStageItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.DisplayNameKey))
            {
                return _localizedTextResolver.Resolve(new LocalizedTextDescriptor(
                    StageDisplayNameKeys.Table,
                    item.DisplayNameKey,
                    LocalizedTextRole.Label));
            }

            return item.StageId.IsValid ? item.StageId.Value : string.Empty;
        }
    }
}
