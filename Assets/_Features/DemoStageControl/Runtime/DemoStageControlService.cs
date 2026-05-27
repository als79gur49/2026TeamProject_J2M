using System;
using System.Collections.Generic;
using Game.Feature.Stages;

namespace Game.Feature.DemoStageControl
{
    public sealed class DemoStageControlService : IDemoStageControlCommandPort
    {
        private readonly IDemoStageControlCampaignBridge _campaignBridge;
        private readonly StageCatalogResolver _catalogResolver;
        private readonly StageCatalogQueryService _catalogQueryService;
        private readonly IDemoStageControlCompletionBridge _completionBridge;
        private readonly IDemoStageControlLaunchBridge _launchBridge;
        private readonly DemoStageControlSettings _settings;
        private string _lastResultMessage = string.Empty;

        public DemoStageControlService(
            DemoStageControlSettings settings,
            IStageCatalogProvider stageCatalogProvider,
            IDemoStageControlCampaignBridge campaignBridge,
            IDemoStageControlLaunchBridge launchBridge,
            IDemoStageControlCompletionBridge completionBridge)
        {
            _settings = settings ?? DemoStageControlSettings.EnabledByDefault();
            _campaignBridge = campaignBridge ?? throw new ArgumentNullException(nameof(campaignBridge));
            _launchBridge = launchBridge ?? throw new ArgumentNullException(nameof(launchBridge));
            _completionBridge = completionBridge ?? throw new ArgumentNullException(nameof(completionBridge));
            if (stageCatalogProvider == null)
            {
                throw new ArgumentNullException(nameof(stageCatalogProvider));
            }

            _catalogResolver = new StageCatalogResolver(stageCatalogProvider);
            _catalogQueryService = new StageCatalogQueryService(stageCatalogProvider);
        }

        public IReadOnlyList<DemoStageControlStageItem> GetStages()
        {
            var catalogItems = _catalogQueryService.EnumerateLaunchCatalogItems();
            var result = new List<DemoStageControlStageItem>(catalogItems.Count);
            var currentStageId = ResolveCurrentStageId();
            for (var i = 0; i < catalogItems.Count; i++)
            {
                var item = catalogItems[i];
                if (!item.StageId.IsValid || !_catalogResolver.TryResolve(item.StageId, out var entry))
                {
                    continue;
                }

                var displayName = string.IsNullOrWhiteSpace(item.DisplayName)
                    ? item.StageId.Value
                    : item.DisplayName;
                result.Add(new DemoStageControlStageItem(
                    item.StageId,
                    displayName,
                    item.StageId.Equals(currentStageId),
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

        private DemoStageControlResult Remember(DemoStageControlResult result)
        {
            _lastResultMessage = result.Message;
            return result;
        }
    }
}
