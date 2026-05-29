using System;
using System.Collections.Generic;
using Game.Feature.Stages;

namespace Game.Feature.DemoStageControl
{
    public sealed class DemoStageControlService : IDemoStageControlCommandPort
    {
        private readonly IDemoStageControlCampaignBridge _campaignBridge;
        private readonly StageCatalogResolver _catalogResolver;
        private readonly IDemoStageControlCompletionBridge _completionBridge;
        private readonly IDemoStageControlLaunchBridge _launchBridge;
        private readonly CampaignStageSequenceResolver _sequenceResolver;
        private readonly DemoStageControlSettings _settings;
        private string _lastResultMessage = string.Empty;

        public DemoStageControlService(
            DemoStageControlSettings settings,
            IStageCatalogProvider stageCatalogProvider,
            CampaignStageSequenceResolver sequenceResolver,
            IDemoStageControlCampaignBridge campaignBridge,
            IDemoStageControlLaunchBridge launchBridge,
            IDemoStageControlCompletionBridge completionBridge)
        {
            _settings = settings ?? DemoStageControlSettings.EnabledByDefault();
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
            _campaignBridge = campaignBridge ?? throw new ArgumentNullException(nameof(campaignBridge));
            _launchBridge = launchBridge ?? throw new ArgumentNullException(nameof(launchBridge));
            _completionBridge = completionBridge ?? throw new ArgumentNullException(nameof(completionBridge));
            if (stageCatalogProvider == null)
            {
                throw new ArgumentNullException(nameof(stageCatalogProvider));
            }

            _catalogResolver = new StageCatalogResolver(stageCatalogProvider);
        }

        public IReadOnlyList<DemoStageControlStageItem> GetStages()
        {
            var sequenceEntries = _sequenceResolver.Entries;
            var result = new List<DemoStageControlStageItem>(sequenceEntries.Count);
            var currentStageId = ResolveCurrentStageId();
            for (var i = 0; i < sequenceEntries.Count; i++)
            {
                var sequenceEntry = sequenceEntries[i];
                if (sequenceEntry == null ||
                    !sequenceEntry.StageId.IsValid ||
                    !_catalogResolver.TryResolve(sequenceEntry.StageId, out var entry))
                {
                    continue;
                }

                result.Add(new DemoStageControlStageItem(
                    sequenceEntry.StageId,
                    ResolveDisplayName(entry, sequenceEntry),
                    sequenceEntry.StageId.Equals(currentStageId),
                    _campaignBridge.IsUnlocked(entry)));
            }

            return result;
        }

        public DemoStageControlStatus GetStatus()
        {
            return new DemoStageControlStatus(
                ResolveCurrentStageId(),
                _campaignBridge.CurrentStageId,
                _launchBridge.IsSceneTransitionInProgress,
                _completionBridge.IsCompletionInProgress,
                _lastResultMessage);
        }

        public DemoStageControlResult StartStage(StageId stageId)
        {
            if (!_settings.Enabled)
            {
                return Remember(DemoStageControlResult.Fail("Demo Stage Control is disabled."));
            }

            if (!stageId.IsValid)
            {
                return Remember(DemoStageControlResult.Fail("Selected stage id is invalid."));
            }

            if (!_sequenceResolver.Contains(stageId))
            {
                return Remember(DemoStageControlResult.Fail(
                    $"Stage '{stageId.Value}' is not part of the campaign sequence."));
            }

            if (!_catalogResolver.TryResolve(stageId, out var entry) || entry == null)
            {
                return Remember(DemoStageControlResult.Fail($"Stage '{stageId.Value}' was not found in the catalog."));
            }

            if (_launchBridge.IsSceneTransitionInProgress)
            {
                return Remember(DemoStageControlResult.Fail("Scene transition is already in progress."));
            }

            if (!_settings.AllowLockedStageSelection && !_campaignBridge.IsUnlocked(entry))
            {
                return Remember(DemoStageControlResult.Fail($"Stage '{stageId.Value}' is locked."));
            }

            if (!_campaignBridge.TrySetActiveStage(entry, out var campaignMessage))
            {
                return Remember(DemoStageControlResult.Fail(campaignMessage));
            }

            var launchResult = _launchBridge.Launch(stageId);
            if (!launchResult.Success)
            {
                var suffix = string.IsNullOrWhiteSpace(campaignMessage) ? string.Empty : $" Campaign was updated: {campaignMessage}";
                return Remember(DemoStageControlResult.Fail($"{launchResult.Message}{suffix}"));
            }

            return Remember(DemoStageControlResult.Ok(
                string.IsNullOrWhiteSpace(campaignMessage)
                    ? launchResult.Message
                    : $"{launchResult.Message} {campaignMessage}"));
        }

        public DemoStageControlResult ForceClearCurrentStage()
        {
            if (!_settings.Enabled)
            {
                return Remember(DemoStageControlResult.Fail("Demo Stage Control is disabled."));
            }

            if (_completionBridge.IsCompletionInProgress)
            {
                return Remember(DemoStageControlResult.Fail("Stage completion is already in progress."));
            }

            return Remember(_completionBridge.ForceClearCurrentStage());
        }

        private StageId ResolveCurrentStageId()
        {
            return StageLaunchContextStore.CurrentStageId.IsValid
                ? StageLaunchContextStore.CurrentStageId
                : _campaignBridge.CurrentStageId;
        }

        private static string ResolveDisplayName(
            StageContentEntry entry,
            CampaignStageSequenceEntry sequenceEntry)
        {
            var presentationDisplayName = entry != null && entry.PresentationDefinition != null
                ? entry.PresentationDefinition.DisplayName
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(presentationDisplayName))
            {
                return presentationDisplayName;
            }

            if (sequenceEntry != null && !string.IsNullOrWhiteSpace(sequenceEntry.DisplayName))
            {
                return sequenceEntry.DisplayName;
            }

            return sequenceEntry != null && sequenceEntry.StageId.IsValid
                ? sequenceEntry.StageId.Value
                : string.Empty;
        }

        private DemoStageControlResult Remember(DemoStageControlResult result)
        {
            _lastResultMessage = result.Message;
            return result;
        }
    }
}
