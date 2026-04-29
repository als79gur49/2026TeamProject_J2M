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

    public sealed class GameplayScreenPresenter
    {
        public GameplayScreenViewModel ViewModel { get; } = new GameplayScreenViewModel();

        public void Apply(GameplayScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.HelpLabel,
                payload.ObjectivesLabel,
                payload.InventoryLabel,
                payload.SettingsLabel);
        }
    }

    public sealed class HelpScreenPresenter
    {
        public HelpScreenViewModel ViewModel { get; } = new HelpScreenViewModel();

        public void Apply(HelpScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(payload.TitleText, payload.DescriptionText, payload.BackLabel);
        }
    }

    public sealed class ObjectiveStatusScreenPresenter : IDisposable
    {
        private readonly ObjectiveStatusPresenter _objectiveStatusPresenter;
        private ObjectiveStatusScreenState _state;
        private string _titleText = ObjectiveStatusScreenPayload.Default.TitleText;

        public ObjectiveStatusScreenPresenter(ObjectiveStatusPresenter objectiveStatusPresenter)
        {
            _objectiveStatusPresenter = objectiveStatusPresenter ?? throw new ArgumentNullException(nameof(objectiveStatusPresenter));
            _state = objectiveStatusPresenter.CurrentState;
            _objectiveStatusPresenter.StateChanged += HandleStateChanged;
            ApplyViewModel();
        }

        public ObjectiveStatusScreenViewModel ViewModel { get; } = new ObjectiveStatusScreenViewModel();

        public void ApplyPayload(ObjectiveStatusScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            _titleText = payload.TitleText;
            ApplyViewModel();
        }

        public ObjectiveInfoPopupPayload BuildInfoPopupPayload()
        {
            if (!_state.HasObjective)
            {
                return new ObjectiveInfoPopupPayload(
                    "Objective Info",
                    "No active objective is configured for this stage.");
            }

            return new ObjectiveInfoPopupPayload(
                "Objective Info",
                $"{BuildObjectiveSummary(_state)} Goal reached: {FormatBoolean(_state.GoalReached)} | All conditions: {FormatBoolean(_state.AllConditionsSatisfied)} | Cleared: {FormatBoolean(_state.IsCleared)}");
        }

        public void Dispose()
        {
            _objectiveStatusPresenter.StateChanged -= HandleStateChanged;
            _objectiveStatusPresenter.Dispose();
        }

        private void HandleStateChanged(ObjectiveStatusScreenState state)
        {
            _state = state;
            ApplyViewModel();
        }

        private void ApplyViewModel()
        {
            ViewModel.SetContent(
                _titleText,
                badgeText: BuildObjectiveBadge(_state),
                summaryText: BuildObjectiveSummary(_state),
                detailText: BuildObjectiveDetailText(_state),
                secondaryText: $"Goal: {FormatBoolean(_state.GoalReached)} | Required: {FormatBoolean(_state.AllConditionsSatisfied)} | Cleared: {FormatBoolean(_state.IsCleared)}");
        }

        private static string BuildObjectiveBadge(ObjectiveStatusScreenState state)
        {
            if (!state.HasObjective)
            {
                return "No Objective";
            }

            if (state.IsCleared)
            {
                return "Cleared";
            }

            if (state.AllConditionsSatisfied)
            {
                return "Ready";
            }

            if (state.GoalReached)
            {
                return "Goal Reached";
            }

            return "Pending";
        }

        private static string BuildObjectiveSummary(ObjectiveStatusScreenState state)
        {
            if (!state.HasObjective)
            {
                return "This stage currently has no active objective.";
            }

            if (!string.IsNullOrWhiteSpace(state.ObjectiveTitle) &&
                !string.IsNullOrWhiteSpace(state.ObjectiveSummary))
            {
                return $"{state.ObjectiveTitle}: {state.ObjectiveSummary}";
            }

            if (!string.IsNullOrWhiteSpace(state.ObjectiveTitle))
            {
                return state.ObjectiveTitle;
            }

            if (!string.IsNullOrWhiteSpace(state.ObjectiveSummary))
            {
                return state.ObjectiveSummary;
            }

            if (state.IsCleared)
            {
                return "The objective chain is fully cleared.";
            }

            if (state.AllConditionsSatisfied)
            {
                return "All objective conditions are currently satisfied.";
            }

            if (state.GoalReached)
            {
                return "Primary goal reached. Waiting on remaining conditions.";
            }

            return "Primary goal is still in progress.";
        }

        private static string BuildObjectiveDetailText(ObjectiveStatusScreenState state)
        {
            if (!state.HasObjective)
            {
                return "No objective conditions are configured for display.";
            }

            return string.IsNullOrWhiteSpace(state.ConditionDetailText)
                ? "No displayable objective conditions."
                : state.ConditionDetailText;
        }

        private static string FormatBoolean(bool value)
        {
            return value ? "Yes" : "No";
        }

    }

    public readonly struct InventoryCatalogPresenterInput
    {
        public InventoryCatalogPresenterInput(
            IReadOnlyList<InventoryCatalogItemInput> sourceItems,
            string selectedItemId)
        {
            SourceItems = sourceItems ?? Array.Empty<InventoryCatalogItemInput>();
            SelectedItemId = selectedItemId;
        }

        public IReadOnlyList<InventoryCatalogItemInput> SourceItems { get; }

        public string SelectedItemId { get; }
    }

    public readonly struct InventoryDetailPresenterInput
    {
        public InventoryDetailPresenterInput(InventoryDetailItemInput selectedItem)
        {
            SelectedItem = selectedItem;
        }

        public InventoryDetailItemInput SelectedItem { get; }
    }

    public readonly struct InventoryActionPresenterInput
    {
        public InventoryActionPresenterInput(
            InventoryActionSelectionInput selectedItem,
            int resetVersion)
        {
            SelectedItem = selectedItem;
            ResetVersion = resetVersion;
        }

        public InventoryActionSelectionInput SelectedItem { get; }

        public int ResetVersion { get; }
    }

    public sealed class InventoryCatalogItemInput
    {
        public InventoryCatalogItemInput(
            string itemId,
            string labelText,
            InventoryItemCategory category,
            int amount,
            string descriptionText)
        {
            ItemId = itemId ?? string.Empty;
            LabelText = labelText ?? string.Empty;
            Category = category;
            Amount = amount;
            DescriptionText = descriptionText ?? string.Empty;
        }

        public string ItemId { get; }

        public string LabelText { get; }

        public InventoryItemCategory Category { get; }

        public int Amount { get; }

        public string DescriptionText { get; }
    }

    public sealed class InventoryDetailItemInput
    {
        public InventoryDetailItemInput(
            string itemId,
            string labelText,
            InventoryItemCategory category,
            int amount,
            string descriptionText,
            string detailText)
        {
            ItemId = itemId ?? string.Empty;
            LabelText = labelText ?? string.Empty;
            Category = category;
            Amount = amount;
            DescriptionText = descriptionText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
        }

        public string ItemId { get; }

        public string LabelText { get; }

        public InventoryItemCategory Category { get; }

        public int Amount { get; }

        public string DescriptionText { get; }

        public string DetailText { get; }
    }

    public sealed class InventoryActionChoiceInput
    {
        public InventoryActionChoiceInput(
            string actionId,
            string labelText,
            string previewFeedbackText,
            bool isEnabled,
            string disabledReasonText)
        {
            ActionId = actionId ?? string.Empty;
            LabelText = labelText ?? string.Empty;
            PreviewFeedbackText = previewFeedbackText ?? string.Empty;
            IsEnabled = isEnabled;
            DisabledReasonText = disabledReasonText ?? string.Empty;
        }

        public string ActionId { get; }

        public string LabelText { get; }

        public string PreviewFeedbackText { get; }

        public bool IsEnabled { get; }

        public string DisabledReasonText { get; }
    }

    public sealed class InventoryActionSelectionInput
    {
        public InventoryActionSelectionInput(
            string itemId,
            string itemLabelText,
            IReadOnlyList<InventoryActionChoiceInput> actions)
        {
            ItemId = itemId ?? string.Empty;
            ItemLabelText = itemLabelText ?? string.Empty;
            Actions = actions ?? Array.Empty<InventoryActionChoiceInput>();
        }

        public string ItemId { get; }

        public string ItemLabelText { get; }

        public IReadOnlyList<InventoryActionChoiceInput> Actions { get; }
    }

    public sealed class InventoryScreenPresenter : IDisposable
    {
        private readonly Dictionary<string, InventoryScreenItemPayload> _itemsById = new Dictionary<string, InventoryScreenItemPayload>(StringComparer.Ordinal);
        private readonly InventoryActionPresenter _actionPresenter;
        private readonly InventoryCatalogPresenter _catalogPresenter;
        private readonly InventoryDetailPresenter _detailPresenter;
        private IReadOnlyList<InventoryScreenItemPayload> _sourceItems = Array.Empty<InventoryScreenItemPayload>();
        private int _actionResetVersion;
        private string _selectedItemId;

        public InventoryScreenPresenter()
            : this(
                new InventoryCatalogPresenter(),
                new InventoryDetailPresenter(),
                new InventoryActionPresenter())
        {
        }

        public InventoryScreenPresenter(
            InventoryCatalogPresenter catalogPresenter,
            InventoryDetailPresenter detailPresenter,
            InventoryActionPresenter actionPresenter)
        {
            _catalogPresenter = catalogPresenter ?? throw new ArgumentNullException(nameof(catalogPresenter));
            _detailPresenter = detailPresenter ?? throw new ArgumentNullException(nameof(detailPresenter));
            _actionPresenter = actionPresenter ?? throw new ArgumentNullException(nameof(actionPresenter));
            _catalogPresenter.SelectionChanged += HandleCatalogSelectionChanged;
        }

        public InventoryScreenViewModel ViewModel { get; } = new InventoryScreenViewModel();

        public InventoryCatalogPresenter CatalogPresenter => _catalogPresenter;

        public InventoryDetailPresenter DetailPresenter => _detailPresenter;

        public InventoryActionPresenter ActionPresenter => _actionPresenter;

        public void Apply(InventoryScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(payload.TitleText, payload.BackLabel);
            _sourceItems = payload.Items ?? Array.Empty<InventoryScreenItemPayload>();
            RebuildLookup(_sourceItems);
            _selectedItemId = _catalogPresenter.Apply(CreateCatalogInput(_sourceItems));
            RefreshSelectionProjection();
        }

        public void Dispose()
        {
            _catalogPresenter.SelectionChanged -= HandleCatalogSelectionChanged;
        }

        private void HandleCatalogSelectionChanged(string selectedItemId)
        {
            if (string.Equals(_selectedItemId, selectedItemId, StringComparison.Ordinal))
            {
                return;
            }

            _selectedItemId = selectedItemId;
            RefreshSelectionProjection();
        }

        private InventoryCatalogPresenterInput CreateCatalogInput(IReadOnlyList<InventoryScreenItemPayload> sourceItems)
        {
            var browseItems = new InventoryCatalogItemInput[sourceItems.Count];
            for (var i = 0; i < sourceItems.Count; i++)
            {
                var item = sourceItems[i];
                browseItems[i] = new InventoryCatalogItemInput(
                    item.ItemId,
                    item.LabelText,
                    item.Category,
                    item.Amount,
                    item.DescriptionText);
            }

            return new InventoryCatalogPresenterInput(browseItems, _selectedItemId);
        }

        private void RefreshSelectionProjection()
        {
            _actionResetVersion++;
            var selectedItem = ResolveSelectedItem();
            if (selectedItem == null)
            {
                _detailPresenter.Apply(new InventoryDetailPresenterInput(null));
                _actionPresenter.Apply(new InventoryActionPresenterInput(null, _actionResetVersion));
                return;
            }

            _detailPresenter.Apply(new InventoryDetailPresenterInput(
                new InventoryDetailItemInput(
                    selectedItem.ItemId,
                    selectedItem.LabelText,
                    selectedItem.Category,
                    selectedItem.Amount,
                    selectedItem.DescriptionText,
                    selectedItem.DetailText)));

            var actions = new InventoryActionChoiceInput[selectedItem.Actions.Count];
            for (var i = 0; i < selectedItem.Actions.Count; i++)
            {
                var action = selectedItem.Actions[i];
                actions[i] = new InventoryActionChoiceInput(
                    action.ActionId,
                    action.LabelText,
                    action.PreviewFeedbackText,
                    action.IsEnabled,
                    action.DisabledReasonText);
            }

            _actionPresenter.Apply(new InventoryActionPresenterInput(
                new InventoryActionSelectionInput(
                    selectedItem.ItemId,
                    selectedItem.LabelText,
                    actions),
                _actionResetVersion));
        }

        private void RebuildLookup(IReadOnlyList<InventoryScreenItemPayload> sourceItems)
        {
            _itemsById.Clear();
            for (var i = 0; i < sourceItems.Count; i++)
            {
                var item = sourceItems[i];
                if (string.IsNullOrEmpty(item.ItemId))
                {
                    continue;
                }

                _itemsById[item.ItemId] = item;
            }
        }

        private InventoryScreenItemPayload ResolveSelectedItem()
        {
            return !string.IsNullOrEmpty(_selectedItemId) &&
                   _itemsById.TryGetValue(_selectedItemId, out var selectedItem)
                ? selectedItem
                : null;
        }
    }

    public sealed class InventoryCatalogPresenter
    {
        private const int MaxRowCount = 5;
        private static readonly string[] SearchTerms = { string.Empty, "Crystal", "Map", "Key" };
        private readonly List<InventoryCatalogItemInput> _visibleItems = new List<InventoryCatalogItemInput>();
        private InventoryCatalogFilterMode _filterMode;
        private InventoryCatalogSortMode _sortMode;
        private IReadOnlyList<InventoryCatalogItemInput> _sourceItems = Array.Empty<InventoryCatalogItemInput>();
        private int _searchIndex;
        private string _selectedItemId;

        public event Action<string> SelectionChanged;

        public InventoryCatalogViewModel ViewModel { get; } = new InventoryCatalogViewModel();

        public string Apply(InventoryCatalogPresenterInput input)
        {
            _sourceItems = input.SourceItems ?? Array.Empty<InventoryCatalogItemInput>();
            _selectedItemId = input.SelectedItemId;
            Refresh(shouldNotifySelectionChange: false);
            return _selectedItemId;
        }

        public void CycleSearch()
        {
            _searchIndex = (_searchIndex + 1) % SearchTerms.Length;
            Refresh(shouldNotifySelectionChange: true);
        }

        public void CycleFilter()
        {
            _filterMode = _filterMode switch
            {
                InventoryCatalogFilterMode.All => InventoryCatalogFilterMode.Consumable,
                InventoryCatalogFilterMode.Consumable => InventoryCatalogFilterMode.Utility,
                InventoryCatalogFilterMode.Utility => InventoryCatalogFilterMode.KeyItem,
                _ => InventoryCatalogFilterMode.All,
            };
            Refresh(shouldNotifySelectionChange: true);
        }

        public void CycleSort()
        {
            _sortMode = _sortMode switch
            {
                InventoryCatalogSortMode.Name => InventoryCatalogSortMode.QuantityDescending,
                InventoryCatalogSortMode.QuantityDescending => InventoryCatalogSortMode.CategoryThenName,
                _ => InventoryCatalogSortMode.Name,
            };
            Refresh(shouldNotifySelectionChange: true);
        }

        public void SelectVisibleRow(int visibleIndex)
        {
            if (visibleIndex < 0 || visibleIndex >= _visibleItems.Count)
            {
                return;
            }

            var nextSelectedItemId = _visibleItems[visibleIndex].ItemId;
            if (string.Equals(_selectedItemId, nextSelectedItemId, StringComparison.Ordinal))
            {
                return;
            }

            _selectedItemId = nextSelectedItemId;
            PublishViewModel();
            SelectionChanged?.Invoke(_selectedItemId);
        }

        private void Refresh(bool shouldNotifySelectionChange)
        {
            _visibleItems.Clear();
            for (var i = 0; i < _sourceItems.Count; i++)
            {
                var item = _sourceItems[i];
                if (MatchesSearch(item) && MatchesFilter(item))
                {
                    _visibleItems.Add(item);
                }
            }

            SortVisibleItems();

            var previousSelection = _selectedItemId;
            if (!ContainsVisibleItem(previousSelection))
            {
                _selectedItemId = _visibleItems.Count > 0 ? _visibleItems[0].ItemId : null;
            }

            PublishViewModel();

            if (shouldNotifySelectionChange &&
                !string.Equals(previousSelection, _selectedItemId, StringComparison.Ordinal))
            {
                SelectionChanged?.Invoke(_selectedItemId);
            }
        }

        private bool ContainsVisibleItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            for (var i = 0; i < _visibleItems.Count; i++)
            {
                if (string.Equals(_visibleItems[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool MatchesFilter(InventoryCatalogItemInput item)
        {
            return _filterMode switch
            {
                InventoryCatalogFilterMode.All => true,
                InventoryCatalogFilterMode.Consumable => item.Category == InventoryItemCategory.Consumable,
                InventoryCatalogFilterMode.Utility => item.Category == InventoryItemCategory.Utility,
                _ => item.Category == InventoryItemCategory.KeyItem,
            };
        }

        private bool MatchesSearch(InventoryCatalogItemInput item)
        {
            var searchTerm = SearchTerms[_searchIndex];
            if (string.IsNullOrEmpty(searchTerm))
            {
                return true;
            }

            return item.LabelText.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.DescriptionText.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   GetCategoryLabel(item.Category).IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void PublishViewModel()
        {
            var rows = new InventoryCatalogRowViewModel[MaxRowCount];
            for (var i = 0; i < MaxRowCount; i++)
            {
                if (i < _visibleItems.Count)
                {
                    var item = _visibleItems[i];
                    rows[i] = new InventoryCatalogRowViewModel(
                        item.LabelText,
                        $"{GetCategoryLabel(item.Category)} | Qty {item.Amount}",
                        isSelected: string.Equals(item.ItemId, _selectedItemId, StringComparison.Ordinal),
                        isVisible: true);
                }
                else
                {
                    rows[i] = new InventoryCatalogRowViewModel(string.Empty, string.Empty, isSelected: false, isVisible: false);
                }
            }

            ViewModel.SetContent(
                $"Search: {GetSearchLabel()}",
                $"Filter: {GetFilterLabel()}",
                $"Sort: {GetSortLabel()}",
                BuildSummaryText(),
                BuildEmptyStateText(),
                rows);
        }

        private void SortVisibleItems()
        {
            _visibleItems.Sort((left, right) =>
            {
                return _sortMode switch
                {
                    InventoryCatalogSortMode.QuantityDescending => CompareByQuantityDescending(left, right),
                    InventoryCatalogSortMode.CategoryThenName => CompareByCategoryThenName(left, right),
                    _ => string.Compare(left.LabelText, right.LabelText, StringComparison.OrdinalIgnoreCase),
                };
            });
        }

        private static int CompareByCategoryThenName(InventoryCatalogItemInput left, InventoryCatalogItemInput right)
        {
            var categoryComparison = left.Category.CompareTo(right.Category);
            if (categoryComparison != 0)
            {
                return categoryComparison;
            }

            return string.Compare(left.LabelText, right.LabelText, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareByQuantityDescending(InventoryCatalogItemInput left, InventoryCatalogItemInput right)
        {
            var amountComparison = right.Amount.CompareTo(left.Amount);
            if (amountComparison != 0)
            {
                return amountComparison;
            }

            return string.Compare(left.LabelText, right.LabelText, StringComparison.OrdinalIgnoreCase);
        }

        private string BuildEmptyStateText()
        {
            return _visibleItems.Count == 0
                ? $"No browse results for {GetSearchLabel()} in {GetFilterLabel()}."
                : string.Empty;
        }

        private string BuildSummaryText()
        {
            return $"Visible {_visibleItems.Count} of {_sourceItems.Count} | {GetFilterLabel()} | {GetSortLabel()}";
        }

        private string GetFilterLabel()
        {
            return _filterMode switch
            {
                InventoryCatalogFilterMode.Consumable => "Consumable",
                InventoryCatalogFilterMode.Utility => "Utility",
                InventoryCatalogFilterMode.KeyItem => "Key Item",
                _ => "All",
            };
        }

        private string GetSearchLabel()
        {
            return string.IsNullOrEmpty(SearchTerms[_searchIndex]) ? "All" : SearchTerms[_searchIndex];
        }

        private string GetSortLabel()
        {
            return _sortMode switch
            {
                InventoryCatalogSortMode.QuantityDescending => "Quantity",
                InventoryCatalogSortMode.CategoryThenName => "Category",
                _ => "Name",
            };
        }

        private static string GetCategoryLabel(InventoryItemCategory category)
        {
            return category switch
            {
                InventoryItemCategory.Consumable => "Consumable",
                InventoryItemCategory.Utility => "Utility",
                _ => "Key Item",
            };
        }

        private enum InventoryCatalogFilterMode
        {
            All = 0,
            Consumable = 1,
            Utility = 2,
            KeyItem = 3,
        }

        private enum InventoryCatalogSortMode
        {
            Name = 0,
            QuantityDescending = 1,
            CategoryThenName = 2,
        }
    }

    public sealed class InventoryDetailPresenter
    {
        public InventoryDetailViewModel ViewModel { get; } = new InventoryDetailViewModel();

        public void Apply(InventoryDetailPresenterInput input)
        {
            if (input.SelectedItem == null)
            {
                ViewModel.SetContent(
                    "No Item Selected",
                    "Awaiting Selection",
                    "Choose an inventory row to inspect its local presentation details.",
                    "Selection stays canonical at the root, but detail projection stays local here.");
                return;
            }

            ViewModel.SetContent(
                input.SelectedItem.LabelText,
                $"{GetCategoryLabel(input.SelectedItem.Category)} | Qty {input.SelectedItem.Amount}",
                input.SelectedItem.DescriptionText,
                input.SelectedItem.DetailText);
        }

        private static string GetCategoryLabel(InventoryItemCategory category)
        {
            return category switch
            {
                InventoryItemCategory.Consumable => "Consumable",
                InventoryItemCategory.Utility => "Utility",
                _ => "Key Item",
            };
        }
    }

    public sealed class InventoryActionPresenter
    {
        private string _feedbackText = string.Empty;
        private int _lastResetVersion = -1;
        private InventoryActionSelectionInput _selectedItem;

        public InventoryActionViewModel ViewModel { get; } = new InventoryActionViewModel();

        public void Apply(InventoryActionPresenterInput input)
        {
            _selectedItem = input.SelectedItem;
            if (_lastResetVersion != input.ResetVersion)
            {
                _feedbackText = string.Empty;
                _lastResetVersion = input.ResetVersion;
            }

            PublishViewModel();
        }

        public void RequestPrimaryAction()
        {
            RequestAction(0);
        }

        public void RequestSecondaryAction()
        {
            RequestAction(1);
        }

        private void RequestAction(int index)
        {
            if (_selectedItem == null)
            {
                _feedbackText = "Select an item to preview available actions.";
                PublishViewModel();
                return;
            }

            if (index < 0 || index >= _selectedItem.Actions.Count)
            {
                return;
            }

            var action = _selectedItem.Actions[index];
            _feedbackText = action.IsEnabled
                ? action.PreviewFeedbackText
                : (string.IsNullOrEmpty(action.DisabledReasonText) ? "Unavailable." : action.DisabledReasonText);
            PublishViewModel();
        }

        private void PublishViewModel()
        {
            var primary = _selectedItem != null && _selectedItem.Actions.Count > 0
                ? _selectedItem.Actions[0]
                : null;
            var secondary = _selectedItem != null && _selectedItem.Actions.Count > 1
                ? _selectedItem.Actions[1]
                : null;
            var feedbackText = _selectedItem == null && string.IsNullOrEmpty(_feedbackText)
                ? "Preview-only actions will appear here after selection."
                : _feedbackText;

            ViewModel.SetContent(
                primary?.LabelText ?? string.Empty,
                BuildActionState(primary),
                isPrimaryVisible: primary != null,
                isPrimaryEnabled: primary != null && primary.IsEnabled,
                secondary?.LabelText ?? string.Empty,
                BuildActionState(secondary),
                isSecondaryVisible: secondary != null,
                isSecondaryEnabled: secondary != null && secondary.IsEnabled,
                feedbackText);
        }

        private static string BuildActionState(InventoryActionChoiceInput action)
        {
            if (action == null)
            {
                return string.Empty;
            }

            if (action.IsEnabled)
            {
                return "Ready";
            }

            return string.IsNullOrEmpty(action.DisabledReasonText)
                ? "Unavailable"
                : action.DisabledReasonText;
        }
    }

    public sealed class AccessibilitySettingsStore
    {
        public SettingsScreenState State { get; private set; } = new SettingsScreenState(
            areTooltipsEnabled: true,
            isLargeTextEnabled: false);

        public void ToggleTooltips()
        {
            State = new SettingsScreenState(!State.AreTooltipsEnabled, State.IsLargeTextEnabled);
        }

        public void ToggleLargeText()
        {
            State = new SettingsScreenState(State.AreTooltipsEnabled, !State.IsLargeTextEnabled);
        }
    }

    public readonly struct SettingsAudioPresenterInput
    {
        public SettingsAudioPresenterInput(
            string mainAudioLabel,
            string bgmAudioLabel,
            string sfxAudioLabel)
        {
            MainAudioLabel = mainAudioLabel ?? string.Empty;
            BgmAudioLabel = bgmAudioLabel ?? string.Empty;
            SfxAudioLabel = sfxAudioLabel ?? string.Empty;
        }

        public string MainAudioLabel { get; }

        public string BgmAudioLabel { get; }

        public string SfxAudioLabel { get; }
    }

    public readonly struct SettingsDisplayPresenterInput
    {
        public SettingsDisplayPresenterInput(
            string displaySectionTitle,
            string currentDisplayLabel,
            string resolutionLabel,
            string resolutionHoverHintText,
            string fullscreenLabel,
            string displayApplyLabel,
            string displayRevertLabel)
        {
            DisplaySectionTitle = displaySectionTitle ?? string.Empty;
            CurrentDisplayLabel = currentDisplayLabel ?? string.Empty;
            ResolutionLabel = resolutionLabel ?? string.Empty;
            ResolutionHoverHintText = resolutionHoverHintText ?? string.Empty;
            FullscreenLabel = fullscreenLabel ?? string.Empty;
            DisplayApplyLabel = displayApplyLabel ?? string.Empty;
            DisplayRevertLabel = displayRevertLabel ?? string.Empty;
        }

        public string DisplaySectionTitle { get; }

        public string CurrentDisplayLabel { get; }

        public string ResolutionLabel { get; }

        public string ResolutionHoverHintText { get; }

        public string FullscreenLabel { get; }

        public string DisplayApplyLabel { get; }

        public string DisplayRevertLabel { get; }
    }

    public sealed class SettingsAudioPresenter
    {
        private readonly IAudioSettingsPort _audioSettingsPort;
        private SettingsAudioPresenterInput _input;

        public SettingsAudioPresenter(IAudioSettingsPort audioSettingsPort)
        {
            _audioSettingsPort = audioSettingsPort ?? throw new ArgumentNullException(nameof(audioSettingsPort));
        }

        public SettingsAudioViewModel ViewModel { get; } = new SettingsAudioViewModel();

        public void Apply(SettingsAudioPresenterInput input)
        {
            _input = input;
            RefreshViewModel();
        }

        public void Flush()
        {
            _audioSettingsPort.Flush();
            RefreshViewModel();
        }

        public void SetMuted(AudioSettingsChannel channel, bool isMuted)
        {
            _audioSettingsPort.SetMuted(channel, isMuted);
            RefreshViewModel();
        }

        public void SetVolume(AudioSettingsChannel channel, float volume)
        {
            _audioSettingsPort.SetVolume(channel, volume);
            RefreshViewModel();
        }

        private void RefreshViewModel()
        {
            var snapshot = _audioSettingsPort.Read();
            ViewModel.SetContent(
                BuildAudioRow(_input.MainAudioLabel, snapshot.Main),
                BuildAudioRow(_input.BgmAudioLabel, snapshot.Bgm),
                BuildAudioRow(_input.SfxAudioLabel, snapshot.Sfx));
        }

        private static AudioSettingsRowViewModel BuildAudioRow(
            string labelText,
            AudioSettingsPortChannelState state)
        {
            var normalizedVolume = Clamp01(state.Volume);
            var percent = (int)Math.Round(normalizedVolume * 100f, MidpointRounding.AwayFromZero);
            var valueText = state.IsMuted
                ? $"{percent}% (Muted)"
                : $"{percent}%";
            return new AudioSettingsRowViewModel(labelText, valueText, normalizedVolume, state.IsMuted);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }
    }

    public readonly struct DisplayPreviewCountdownSnapshot
    {
        public static DisplayPreviewCountdownSnapshot Inactive => default;

        public DisplayPreviewCountdownSnapshot(bool isActive, int remainingSeconds, int totalSeconds)
        {
            IsActive = isActive;
            RemainingSeconds = remainingSeconds;
            TotalSeconds = totalSeconds;
        }

        public bool IsActive { get; }

        public int RemainingSeconds { get; }

        public int TotalSeconds { get; }

        public static DisplayPreviewCountdownSnapshot Create(double remainingSeconds, double totalSeconds)
        {
            var totalWholeSeconds = ComputeVisibleSeconds(totalSeconds, totalSeconds);
            if (totalWholeSeconds <= 0)
            {
                return Inactive;
            }

            var visibleRemainingSeconds = ComputeVisibleSeconds(remainingSeconds, totalWholeSeconds);
            if (visibleRemainingSeconds <= 0)
            {
                return Inactive;
            }

            return new DisplayPreviewCountdownSnapshot(
                isActive: true,
                remainingSeconds: visibleRemainingSeconds,
                totalSeconds: totalWholeSeconds);
        }

        public static int ComputeVisibleSeconds(double remainingSeconds, double totalSeconds)
        {
            var totalWholeSeconds = Math.Max(0, (int)Math.Ceiling(Math.Max(0d, totalSeconds)));
            if (totalWholeSeconds <= 0 || remainingSeconds <= 0d)
            {
                return 0;
            }

            if (remainingSeconds <= 1d)
            {
                return 1;
            }

            return Math.Min(totalWholeSeconds, (int)Math.Floor(remainingSeconds) + 1);
        }
    }

    public sealed class SettingsDisplayPresenter
    {
        private const string PreviewRevertedStatusText =
            "Preview reverted to the previous saved display settings.";
        private const string PreviewCommittedStatusText =
            "Display settings saved.";
        private const string ExternalDriftStatusText =
            "Current display changed outside saved settings. Saved settings remain unchanged until you apply again.";

        private readonly IDisplaySettingsPort _displaySettingsPort;
        private DisplaySettingsPortSnapshot _displaySnapshot = new(
            Array.Empty<DisplaySettingsPortModeOption>(),
            0,
            DisplayWindowMode.Windowed,
            string.Empty,
            DisplayWindowMode.Windowed,
            false);
        private SettingsDisplayPresenterInput _input;
        private int _stagedDisplayModeIndex;
        private DisplayWindowMode _stagedDisplayWindowMode;
        private string _displayStatusText = string.Empty;
        private DisplayPreviewCountdownSnapshot _previewCountdown = DisplayPreviewCountdownSnapshot.Inactive;

        public SettingsDisplayPresenter(IDisplaySettingsPort displaySettingsPort)
        {
            _displaySettingsPort = displaySettingsPort ?? throw new ArgumentNullException(nameof(displaySettingsPort));
        }

        public SettingsDisplayViewModel ViewModel { get; } = new SettingsDisplayViewModel();

        public void Apply(SettingsDisplayPresenterInput input, double previewTimeoutSeconds)
        {
            _input = input;
            ClearPreviewCountdown();
            ResyncState(resetStagedToCommitted: true, previewTimeoutSeconds: previewTimeoutSeconds);
        }

        public bool ApplyStagedSettings(double previewTimeoutSeconds)
        {
            if (_displaySnapshot.IsPreviewActive || !IsDirty())
            {
                return false;
            }

            var started = _displaySettingsPort.BeginPreview(
                new DisplaySettingsPortPreviewRequest(_stagedDisplayModeIndex, _stagedDisplayWindowMode));
            if (!started)
            {
                ClearPreviewCountdown();
            }

            ResyncState(
                resetStagedToCommitted: false,
                previewTimeoutSeconds: previewTimeoutSeconds,
                overrideStatusText: started ? BuildPreviewActiveStatusText(previewTimeoutSeconds) : null);
            return started;
        }

        public bool CancelPreview()
        {
            var reverted = _displaySettingsPort.RevertPreview();
            ClearPreviewCountdown();
            ResyncState(
                resetStagedToCommitted: true,
                previewTimeoutSeconds: 0d,
                overrideStatusText: reverted ? PreviewRevertedStatusText : null);
            return reverted;
        }

        public bool ConfirmPreview()
        {
            var committed = _displaySettingsPort.CommitPreview();
            ClearPreviewCountdown();
            ResyncState(
                resetStagedToCommitted: true,
                previewTimeoutSeconds: 0d,
                overrideStatusText: committed ? PreviewCommittedStatusText : null);
            return committed;
        }

        public void ClearPreviewCountdown()
        {
            _previewCountdown = DisplayPreviewCountdownSnapshot.Inactive;
            RefreshViewModel();
        }

        public void ResetStagedToCurrent()
        {
            if (_displaySnapshot.IsPreviewActive)
            {
                return;
            }

            _stagedDisplayModeIndex = ClampDisplayModeIndex(_displaySnapshot.CommittedModeIndex, _displaySnapshot.AvailableModes.Count);
            _stagedDisplayWindowMode = _displaySnapshot.CommittedWindowMode;
            RefreshViewModel();
        }

        public void ResyncState(double previewTimeoutSeconds)
        {
            ResyncState(resetStagedToCommitted: false, previewTimeoutSeconds: previewTimeoutSeconds);
        }

        public void SetPreviewCountdown(DisplayPreviewCountdownSnapshot snapshot)
        {
            _previewCountdown = snapshot;
            RefreshViewModel();
        }

        public void StageResolution(int modeIndex)
        {
            if (_displaySnapshot.IsPreviewActive || _displaySnapshot.AvailableModes.Count == 0)
            {
                return;
            }

            _stagedDisplayModeIndex = ClampDisplayModeIndex(modeIndex, _displaySnapshot.AvailableModes.Count);
            RefreshViewModel();
        }

        public void StageWindowMode(DisplayWindowMode mode)
        {
            if (_displaySnapshot.IsPreviewActive)
            {
                return;
            }

            _stagedDisplayWindowMode = mode;
            RefreshViewModel();
        }

        private void ResyncState(
            bool resetStagedToCommitted,
            double previewTimeoutSeconds,
            string overrideStatusText = null)
        {
            _displaySnapshot = _displaySettingsPort.Read();
            if (_displaySnapshot.AvailableModes.Count == 0)
            {
                _displaySnapshot = new DisplaySettingsPortSnapshot(
                    Array.Empty<DisplaySettingsPortModeOption>(),
                    0,
                    _displaySnapshot.CommittedWindowMode,
                    _displaySnapshot.CurrentRuntimeResolutionLabel,
                    _displaySnapshot.CurrentRuntimeWindowMode,
                    _displaySnapshot.IsPreviewActive);
            }

            if (resetStagedToCommitted || _displaySnapshot.AvailableModes.Count == 0)
            {
                _stagedDisplayModeIndex = ClampDisplayModeIndex(_displaySnapshot.CommittedModeIndex, _displaySnapshot.AvailableModes.Count);
                _stagedDisplayWindowMode = _displaySnapshot.CommittedWindowMode;
            }
            else
            {
                _stagedDisplayModeIndex = ClampDisplayModeIndex(_stagedDisplayModeIndex, _displaySnapshot.AvailableModes.Count);
            }

            _displayStatusText = overrideStatusText ?? BuildDisplayStatusText(previewTimeoutSeconds);
            RefreshViewModel();
        }

        private static string BuildPreviewActiveStatusText(double previewTimeoutSeconds)
        {
            var visibleTimeoutSeconds = DisplayPreviewCountdownSnapshot.ComputeVisibleSeconds(
                previewTimeoutSeconds,
                previewTimeoutSeconds);
            return $"Preview active. Current display is temporary and not saved. Confirm to keep it, or it will revert in {visibleTimeoutSeconds} seconds.";
        }

        private string BuildDisplayStatusText(double previewTimeoutSeconds)
        {
            if (_displaySnapshot.IsPreviewActive)
            {
                return BuildPreviewActiveStatusText(previewTimeoutSeconds);
            }

            if (_displaySnapshot.CurrentRuntimeWindowMode != _displaySnapshot.CommittedWindowMode)
            {
                return ExternalDriftStatusText;
            }

            if (_displaySnapshot.AvailableModes.Count == 0)
            {
                return string.Empty;
            }

            var committedIndex = ClampDisplayModeIndex(_displaySnapshot.CommittedModeIndex, _displaySnapshot.AvailableModes.Count);
            if (_displaySnapshot.CurrentRuntimeResolutionLabel != _displaySnapshot.AvailableModes[committedIndex].LabelText)
            {
                return ExternalDriftStatusText;
            }

            return string.Empty;
        }

        private void RefreshViewModel()
        {
            var resolutionOptions = new List<string>(_displaySnapshot.AvailableModes.Count);
            for (var i = 0; i < _displaySnapshot.AvailableModes.Count; i++)
            {
                resolutionOptions.Add(_displaySnapshot.AvailableModes[i].LabelText);
            }

            var isPreviewCountdownVisible = _displaySnapshot.IsPreviewActive &&
                                            _previewCountdown.IsActive &&
                                            _previewCountdown.TotalSeconds > 0 &&
                                            _previewCountdown.RemainingSeconds > 0;
            var previewCountdownText = isPreviewCountdownVisible
                ? $"Reverting in {_previewCountdown.RemainingSeconds}s"
                : string.Empty;
            var previewCountdownNormalized = isPreviewCountdownVisible
                ? Clamp01((float)_previewCountdown.RemainingSeconds / _previewCountdown.TotalSeconds)
                : 0f;

            ViewModel.SetContent(
                _input.DisplaySectionTitle,
                _input.CurrentDisplayLabel,
                _displaySnapshot.CurrentRuntimeResolutionLabel,
                _input.ResolutionLabel,
                _input.ResolutionHoverHintText,
                resolutionOptions,
                _stagedDisplayModeIndex,
                _input.FullscreenLabel,
                _stagedDisplayWindowMode == DisplayWindowMode.FullScreenWindow,
                _displayStatusText,
                _input.DisplayApplyLabel,
                IsDirty() && !_displaySnapshot.IsPreviewActive,
                _input.DisplayRevertLabel,
                IsDirty() && !_displaySnapshot.IsPreviewActive,
                _displaySnapshot.IsPreviewActive,
                previewCountdownText,
                previewCountdownNormalized,
                isPreviewCountdownVisible);
        }

        private bool IsDirty()
        {
            return _stagedDisplayModeIndex != ClampDisplayModeIndex(_displaySnapshot.CommittedModeIndex, _displaySnapshot.AvailableModes.Count) ||
                   _stagedDisplayWindowMode != _displaySnapshot.CommittedWindowMode;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }

        private static int ClampDisplayModeIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            if (index >= count)
            {
                return count - 1;
            }

            return index;
        }
    }

    public readonly struct SettingsInputPresenterInput
    {
        public SettingsInputPresenterInput(
            string sectionTitle,
            string movementLabel,
            string useArrowKeysLabel,
            string pushLabel,
            string flipLabel,
            string changeLabel,
            string resetLabel)
        {
            SectionTitle = sectionTitle ?? string.Empty;
            MovementLabel = movementLabel ?? string.Empty;
            UseArrowKeysLabel = useArrowKeysLabel ?? string.Empty;
            PushLabel = pushLabel ?? string.Empty;
            FlipLabel = flipLabel ?? string.Empty;
            ChangeLabel = changeLabel ?? string.Empty;
            ResetLabel = resetLabel ?? string.Empty;
        }

        public string SectionTitle { get; }

        public string MovementLabel { get; }

        public string UseArrowKeysLabel { get; }

        public string PushLabel { get; }

        public string FlipLabel { get; }

        public string ChangeLabel { get; }

        public string ResetLabel { get; }
    }

    public sealed class SettingsInputPresenter
    {
        private readonly IKeyboardBindingSettingsPort _keyboardBindingSettingsPort;
        private SettingsInputPresenterInput _input = new SettingsInputPresenterInput(
            "Input",
            "Movement Keys",
            "Use Arrow Keys",
            "Push",
            "Flip",
            "Change",
            "Reset Input");
        private string _statusText = string.Empty;

        public SettingsInputPresenter(IKeyboardBindingSettingsPort keyboardBindingSettingsPort)
        {
            _keyboardBindingSettingsPort = keyboardBindingSettingsPort ?? throw new ArgumentNullException(nameof(keyboardBindingSettingsPort));
        }

        public SettingsInputViewModel ViewModel { get; } = new SettingsInputViewModel();

        public bool IsRebinding => _keyboardBindingSettingsPort.IsRebinding;

        public void Apply(SettingsInputPresenterInput input)
        {
            _input = input;
            RefreshViewModel(_keyboardBindingSettingsPort.Read());
        }

        public void SetMovementScheme(KeyboardMovementScheme scheme)
        {
            var result = _keyboardBindingSettingsPort.TrySetMovementScheme(scheme);
            _statusText = ToStatusText(result, KeyboardBindableAction.Push);
            RefreshViewModel(_keyboardBindingSettingsPort.Read());
        }

        public void StartRebind(KeyboardBindableAction action)
        {
            var startResult = _keyboardBindingSettingsPort.StartRebind(action, HandleRebindCompleted);
            if (startResult.Started)
            {
                _statusText = action == KeyboardBindableAction.Push
                    ? "Press a key for Push..."
                    : "Press a key for Flip...";
                RefreshViewModel(startResult.Snapshot);
                return;
            }

            _statusText = ToStatusText(startResult.ValidationResult, action);
            RefreshViewModel(startResult.Snapshot);
        }

        public void CancelRebind()
        {
            _keyboardBindingSettingsPort.CancelRebind();
            RefreshViewModel(_keyboardBindingSettingsPort.Read());
        }

        public void ResetToDefaults()
        {
            var snapshot = _keyboardBindingSettingsPort.ResetToDefaults();
            _statusText = "Input settings reset.";
            RefreshViewModel(snapshot);
        }

        private void HandleRebindCompleted(KeyboardRebindResult result)
        {
            _statusText = ToStatusText(result.ValidationResult, result.Action);
            RefreshViewModel(result.Snapshot);
        }

        private void RefreshViewModel(KeyboardBindingSettingsSnapshot snapshot)
        {
            var areControlsInteractable = !snapshot.IsRebinding;
            ViewModel.SetContent(
                _input.SectionTitle,
                _input.MovementLabel,
                _input.UseArrowKeysLabel,
                snapshot.MovementScheme == KeyboardMovementScheme.ArrowKeys,
                snapshot.MovementDisplayName,
                _input.PushLabel,
                snapshot.PushDisplayName,
                _input.ChangeLabel,
                _input.FlipLabel,
                snapshot.FlipDisplayName,
                _input.ChangeLabel,
                _input.ResetLabel,
                _statusText,
                snapshot.IsRebinding,
                areControlsInteractable);
        }

        private static string ToStatusText(KeyboardBindingValidationResult result, KeyboardBindableAction action)
        {
            switch (result)
            {
                case KeyboardBindingValidationResult.Success:
                    return string.Empty;
                case KeyboardBindingValidationResult.Canceled:
                    return "Rebind canceled.";
                case KeyboardBindingValidationResult.ReservedKey:
                    return "This key is reserved.";
                case KeyboardBindingValidationResult.DuplicateAction:
                    return action == KeyboardBindableAction.Push
                        ? "This key is already used by Flip."
                        : "This key is already used by Push.";
                case KeyboardBindingValidationResult.MovementConflict:
                    return "This key conflicts with movement keys.";
                case KeyboardBindingValidationResult.AlreadyRebinding:
                    return "Rebind already in progress.";
                default:
                    return "This key cannot be used.";
            }
        }
    }

    public sealed class SettingsScreenPresenter
    {
        private readonly AccessibilitySettingsStore _accessibilitySettingsStore;
        private SettingsScreenPayload _payload = SettingsScreenPayload.Default;
        private SettingsSectionId _selectedSection = SettingsSectionId.Audio;

        public SettingsScreenPresenter(
            AccessibilitySettingsStore accessibilitySettingsStore,
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort)
            : this(
                accessibilitySettingsStore,
                audioSettingsPort,
                displaySettingsPort,
                NoOpKeyboardBindingSettingsPort.Instance)
        {
        }

        public SettingsScreenPresenter(
            AccessibilitySettingsStore accessibilitySettingsStore,
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort,
            IKeyboardBindingSettingsPort keyboardBindingSettingsPort)
        {
            _accessibilitySettingsStore = accessibilitySettingsStore ?? throw new ArgumentNullException(nameof(accessibilitySettingsStore));
            AudioPresenter = new SettingsAudioPresenter(audioSettingsPort ?? throw new ArgumentNullException(nameof(audioSettingsPort)));
            DisplayPresenter = new SettingsDisplayPresenter(displaySettingsPort ?? throw new ArgumentNullException(nameof(displaySettingsPort)));
            InputPresenter = new SettingsInputPresenter(keyboardBindingSettingsPort ?? throw new ArgumentNullException(nameof(keyboardBindingSettingsPort)));
        }

        public SettingsAudioPresenter AudioPresenter { get; }

        public SettingsDisplayPresenter DisplayPresenter { get; }

        public SettingsInputPresenter InputPresenter { get; }

        public SettingsScreenViewModel ViewModel { get; } = new SettingsScreenViewModel();

        public void Apply(SettingsScreenPayload payload, double previewTimeoutSeconds)
        {
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
            AudioPresenter.Apply(new SettingsAudioPresenterInput(
                _payload.MainAudioLabel,
                _payload.BgmAudioLabel,
                _payload.SfxAudioLabel));
            DisplayPresenter.Apply(new SettingsDisplayPresenterInput(
                _payload.DisplaySectionTitle,
                _payload.CurrentDisplayLabel,
                _payload.ResolutionLabel,
                _payload.ResolutionHoverHintText,
                _payload.FullscreenLabel,
                _payload.DisplayApplyLabel,
                _payload.DisplayRevertLabel),
                previewTimeoutSeconds);
            InputPresenter.Apply(new SettingsInputPresenterInput(
                _payload.InputSectionTitle,
                _payload.MovementLabel,
                _payload.UseArrowKeysLabel,
                _payload.PushLabel,
                _payload.FlipLabel,
                _payload.InputChangeLabel,
                _payload.ResetInputLabel));
            RefreshViewModel();
        }

        public bool SelectSection(SettingsSectionId sectionId)
        {
            if (_selectedSection == sectionId)
            {
                return false;
            }

            _selectedSection = sectionId;
            RefreshViewModel();
            return true;
        }

        public TooltipPopupPayload BuildTooltipInfoPayload()
        {
            var tooltipsEnabledText = _accessibilitySettingsStore.State.AreTooltipsEnabled ? "Enabled" : "Disabled";
            return new TooltipPopupPayload(
                "Tooltips",
                $"Tooltips show short contextual hints for UI controls. They are currently {tooltipsEnabledText} in this session; use {_payload.TooltipToggleLabel} to change that.",
                TooltipPopupAnchorPreset.Center);
        }

        public void ToggleLargeText()
        {
            _accessibilitySettingsStore.ToggleLargeText();
            RefreshViewModel();
        }

        public void ToggleTooltips()
        {
            _accessibilitySettingsStore.ToggleTooltips();
            RefreshViewModel();
        }

        private void RefreshViewModel()
        {
            var accessibilityState = _accessibilitySettingsStore.State;
            ViewModel.SetContent(
                _payload.TitleText,
                accessibilityState.AreTooltipsEnabled ? "Enabled" : "Disabled",
                accessibilityState.IsLargeTextEnabled ? "Enabled" : "Disabled",
                _payload.TooltipToggleLabel,
                _payload.LargeTextToggleLabel,
                _payload.BackLabel,
                _payload.AudioTabLabel,
                _payload.DisplayTabLabel,
                _payload.InputTabLabel,
                _selectedSection);
        }
    }

    public sealed class StageResultScreenPresenter
    {
        public StageResultScreenViewModel ViewModel { get; } = new StageResultScreenViewModel();

        public void Apply(StageResultScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.SummaryText,
                payload.DetailText,
                payload.ContinueLabel);
        }
    }

    public sealed class LevelFailedScreenPresenter
    {
        public LevelFailedScreenViewModel ViewModel { get; } = new LevelFailedScreenViewModel();

        public void Apply(LevelFailedScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.DetailText,
                payload.RestartLevelLabel,
                payload.MainLabel);
        }
    }
}
