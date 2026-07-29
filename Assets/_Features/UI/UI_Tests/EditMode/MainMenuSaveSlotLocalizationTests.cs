using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition.Editor;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Game.Feature.UI.Tests
{
    public sealed class MainMenuStringTableLocalizationTests
    {
        [Test]
        public void MainMenuContract_IsUniqueCompleteAndMatchesTablesBootstrapAndFallbacks()
        {
            var entries = MainMenuLocalizationContract.Entries;
            Assert.That(entries, Has.Count.EqualTo(37));
            Assert.That(entries.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(entries.Count));
            Assert.That(entries.All(entry => !string.IsNullOrWhiteSpace(entry.English)), Is.True);
            Assert.That(entries.All(entry => !string.IsNullOrWhiteSpace(entry.Korean)), Is.True);

            var collection = LocalizationEditorSettings.GetStringTableCollection(MainMenuLocalizationContract.Table);
            Assert.That(collection, Is.Not.Null);
            var englishTable = collection.GetTable("en-US") as StringTable;
            var koreanTable = collection.GetTable("ko-KR") as StringTable;
            Assert.That(englishTable, Is.Not.Null);
            Assert.That(koreanTable, Is.Not.Null);

            var bootstrap = SettingsLocalizationAssetBootstrap.Entries.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
            var packageFree = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var invariantType = typeof(MainMenuController).Assembly.GetType(
                "Game.Feature.UI.Application.InvariantSettingsLocalizedTextResolver");
            var invariant = invariantType
                ?.GetField("Instance", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null) as ILocalizedTextResolver;
            Assert.That(invariant, Is.Not.Null);

            foreach (var entry in entries)
            {
                var english = englishTable.GetEntry(entry.Key);
                var korean = koreanTable.GetEntry(entry.Key);
                Assert.That(english, Is.Not.Null, $"en-US missing {entry.Key}");
                Assert.That(korean, Is.Not.Null, $"ko-KR missing {entry.Key}");
                Assert.That(english.KeyId, Is.EqualTo(korean.KeyId), entry.Key);
                Assert.That(english.LocalizedValue, Is.EqualTo(entry.English), entry.Key);
                Assert.That(korean.LocalizedValue, Is.EqualTo(entry.Korean), entry.Key);
                Assert.That(english.IsSmart, Is.EqualTo(entry.IsSmart), entry.Key);
                Assert.That(korean.IsSmart, Is.EqualTo(entry.IsSmart), entry.Key);
                Assert.That(bootstrap.ContainsKey(entry.Key), Is.True, entry.Key);
                Assert.That(bootstrap[entry.Key].English, Is.EqualTo(entry.English), entry.Key);
                Assert.That(bootstrap[entry.Key].Korean, Is.EqualTo(entry.Korean), entry.Key);
                Assert.That(bootstrap[entry.Key].IsSmart, Is.EqualTo(entry.IsSmart), entry.Key);

                var descriptor = MainMenuLocalization.Descriptor(
                    entry.Id,
                    entry.IsSmart ? new object[] { "ARG" } : Array.Empty<object>());
                Assert.That(
                    packageFree.Resolve(descriptor),
                    Is.EqualTo(entry.IsSmart ? entry.English.Replace("{0}", "ARG") : entry.English),
                    entry.Key);
                Assert.That(
                    invariant.Resolve(descriptor),
                    Is.EqualTo(entry.IsSmart ? entry.English.Replace("{0}", "ARG") : entry.English),
                    entry.Key);
            }

            packageFree.SetLocale("ko-KR");
            foreach (var entry in entries)
            {
                var descriptor = MainMenuLocalization.Descriptor(
                    entry.Id,
                    entry.IsSmart ? new object[] { "ARG" } : Array.Empty<object>());
                Assert.That(
                    packageFree.Resolve(descriptor),
                    Is.EqualTo(entry.IsSmart ? entry.Korean.Replace("{0}", "ARG") : entry.Korean),
                    entry.Key);
            }
        }

        [Test]
        public void MainMenuSmartEntries_UseMatchingSinglePositionalPlaceholder()
        {
            foreach (var entry in MainMenuLocalizationContract.Entries)
            {
                var englishArguments = PlaceholderIndices(entry.English);
                var koreanArguments = PlaceholderIndices(entry.Korean);
                Assert.That(koreanArguments, Is.EqualTo(englishArguments), entry.Key);
                Assert.That(entry.IsSmart, Is.EqualTo(englishArguments.Count > 0), entry.Key);
                Assert.That(englishArguments.All(index => index == 0), Is.True, entry.Key);
            }
        }

        private static IReadOnlyList<int> PlaceholderIndices(string value)
        {
            var result = new List<int>();
            for (var i = 0; i + 2 < value.Length; i++)
            {
                if (value[i] == '{' && char.IsDigit(value[i + 1]) && value[i + 2] == '}')
                {
                    result.Add(value[i + 1] - '0');
                }
            }

            return result;
        }
    }

    public sealed class MainMenuSaveSlotLocalizationTests
    {
        [TestCase(SaveSlotFailurePresentationKind.UnsupportedVersion, "Unsupported Save", "This save was created by an unsupported version.", "지원하지 않는 저장 데이터", "지원하지 않는 버전에서 생성된 저장 데이터입니다.")]
        [TestCase(SaveSlotFailurePresentationKind.CorruptedData, "Save Data Damaged", "This save data could not be read.", "저장 데이터 손상", "저장 데이터를 읽을 수 없습니다.")]
        [TestCase(SaveSlotFailurePresentationKind.PermissionDenied, "Save Access Failed", "The save data could not be accessed. Check file permissions.", "저장 데이터 접근 실패", "저장 데이터 접근 권한을 확인하세요.")]
        [TestCase(SaveSlotFailurePresentationKind.LoadFailed, "Save Load Failed", "The save data could not be loaded.", "저장 불러오기 실패", "저장 데이터를 불러올 수 없습니다.")]
        [TestCase(SaveSlotFailurePresentationKind.NeedsRepair, "Save Data Unavailable", "This save cannot be used in its current state.", "저장 데이터 사용 불가", "현재 상태에서는 이 저장 데이터를 사용할 수 없습니다.")]
        public void FailureDescriptors_ResolveEnglishAndKoreanWithoutBlankText(
            SaveSlotFailurePresentationKind kind,
            string englishTitle,
            string englishDetail,
            string koreanTitle,
            string koreanDetail)
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var descriptor = MainMenuLocalization.FailureDescriptor(kind);

            Assert.That(resolver.Resolve(descriptor.Title), Is.EqualTo(englishTitle));
            Assert.That(resolver.Resolve(descriptor.Detail), Is.EqualTo(englishDetail));

            resolver.SetLocale("ko-KR");
            Assert.That(resolver.Resolve(descriptor.Title), Is.EqualTo(koreanTitle));
            Assert.That(resolver.Resolve(descriptor.Detail), Is.EqualTo(koreanDetail));
            Assert.That(koreanTitle, Is.Not.Empty);
            Assert.That(koreanDetail, Is.Not.Empty);
        }

        [Test]
        public void SlotMapper_ResolvesEmptyInProgressAndCompletedCardsInEnglishAndKorean()
        {
            var sequence = new CampaignStageSequenceResolver(
                CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());
            var slots = new[]
            {
                SaveSlotData.CreateEmpty(1),
                new SaveSlotData
                {
                    SlotNumber = 2,
                    CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                    CurrentLevelGroupId = "level-2",
                    RemainingChances = 2,
                    TotalDeaths = 3,
                    LastPlayedAt = "2026-07-29T12:34:00+09:00",
                },
                new SaveSlotData
                {
                    SlotNumber = 3,
                    CurrentStageId = StageId.CreateOrThrow("stage-4-2"),
                    CurrentLevelGroupId = "level-4",
                    RemainingChances = 1,
                    TotalDeaths = 5,
                    LastPlayedAt = "2026-07-29T12:34:00+09:00",
                    CampaignCompleted = true,
                },
            };
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();

            var english = MainMenuSlotViewModelMapper.Map(slots, sequence, null, resolver);
            Assert.That(english.SlotCards[0].TitleText, Is.EqualTo("Slot 1"));
            Assert.That(english.SlotCards[0].StatusText, Is.EqualTo("Empty"));
            Assert.That(english.SlotCards[0].PrimaryActionText, Is.EqualTo("New Game"));
            Assert.That(english.SlotCards[1].StatusText, Is.EqualTo("Continue"));
            Assert.That(english.SlotCards[1].StageText, Is.EqualTo("Stage Ward[A]-02"));
            Assert.That(english.SlotCards[1].ChancesText, Is.EqualTo("Chances 2"));
            Assert.That(english.SlotCards[1].DeathsText, Is.EqualTo("Deaths 3"));
            Assert.That(english.SlotCards[1].LastPlayedText, Is.EqualTo("Played Jul 29, 2026"));
            Assert.That(english.SlotCards[1].DeleteActionText, Is.EqualTo("Delete"));
            Assert.That(english.SlotCards[2].StatusText, Is.EqualTo("Completed"));
            Assert.That(english.SlotCards[2].PrimaryActionText, Is.EqualTo("Restart"));

            resolver.SetLocale("ko-KR");
            var korean = MainMenuSlotViewModelMapper.Map(slots, sequence, null, resolver);
            Assert.That(korean.SlotCards[0].TitleText, Is.EqualTo("슬롯 1"));
            Assert.That(korean.SlotCards[0].StatusText, Is.EqualTo("비어 있음"));
            Assert.That(korean.SlotCards[0].PrimaryActionText, Is.EqualTo("새 게임"));
            Assert.That(korean.SlotCards[1].StatusText, Is.EqualTo("계속"));
            Assert.That(korean.SlotCards[1].StageText, Is.EqualTo("스테이지 Ward[A]-02"));
            Assert.That(korean.SlotCards[1].ChancesText, Is.EqualTo("기회 2"));
            Assert.That(korean.SlotCards[1].DeathsText, Is.EqualTo("사망 3"));
            Assert.That(korean.SlotCards[1].LastPlayedText, Is.EqualTo("최근 플레이 2026. 7. 29."));
            Assert.That(korean.SlotCards[1].DeleteActionText, Is.EqualTo("삭제"));
            Assert.That(korean.SlotCards[2].StatusText, Is.EqualTo("완료"));
            Assert.That(korean.SlotCards[2].PrimaryActionText, Is.EqualTo("다시 시작"));
        }

        [Test]
        public void SlotMapper_StageTextResolvesThroughStageDescriptorInsteadOfSequenceDisplayName()
        {
            var definition = UnityEngine.ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            try
            {
                var entry = new CampaignStageSequenceEntry();
                entry.Set(StageId.CreateOrThrow("stage-0-1"), "RAW-SEQUENCE-NAME", "level-0");
                definition.SetEntries(new[] { entry });
                var card = MainMenuSlotViewModelMapper.MapSlot(
                    new SaveSlotData
                    {
                        SlotNumber = 1,
                        CurrentStageId = StageId.CreateOrThrow("stage-0-1"),
                        CurrentLevelGroupId = "level-0",
                    },
                    new CampaignStageSequenceResolver(definition),
                    null,
                    PackageFreeLocalizedTextResolver.CreateSettingsDefault());

                Assert.That(card.StageText, Is.EqualTo("Stage Lab-01"));
                Assert.That(card.StageText, Does.Not.Contain("RAW-SEQUENCE-NAME"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }
    }

    public sealed class MainMenuConfirmationLocalizationTests
    {
        [TestCase(MainMenuConfirmationKind.DeleteSlot, "Delete Slot", "Delete slot 2?", "This action cannot be undone.", "Delete")]
        [TestCase(MainMenuConfirmationKind.RestartSlot, "Restart Slot", "Restart slot 2 from the beginning?", "Existing progress will be replaced.", "Restart")]
        [TestCase(MainMenuConfirmationKind.OverwriteSlot, "Overwrite Slot", "Start a new game in slot 2?", "Existing progress will be overwritten.", "New Game")]
        [TestCase(MainMenuConfirmationKind.QuitGame, "Quit Game", "Quit to desktop?", "Unsaved progress may be lost.", "Quit")]
        public void ConfirmationPresenter_ResolvesEnglishCopy(
            MainMenuConfirmationKind kind,
            string title,
            string body,
            string warning,
            string confirm)
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            using var presenter = new ConfirmPopupPresenter(resolver);
            presenter.Apply(MainMenuLocalization.CreateConfirmationPayload(
                kind,
                kind == MainMenuConfirmationKind.QuitGame ? null : 2));

            Assert.That(presenter.ViewModel.TitleText, Is.EqualTo(title));
            Assert.That(presenter.ViewModel.BodyText, Is.EqualTo(body));
            Assert.That(presenter.ViewModel.WarningText, Is.EqualTo(warning));
            Assert.That(presenter.ViewModel.ConfirmLabel, Is.EqualTo(confirm));
            Assert.That(presenter.ViewModel.CancelLabel, Is.EqualTo("Cancel"));
        }

        [TestCase(MainMenuConfirmationKind.DeleteSlot, "슬롯 삭제", "2번 슬롯을 삭제할까요?", "이 작업은 되돌릴 수 없습니다.", "삭제")]
        [TestCase(MainMenuConfirmationKind.RestartSlot, "슬롯 다시 시작", "2번 슬롯을 처음부터 다시 시작할까요?", "기존 진행 상황은 교체됩니다.", "다시 시작")]
        [TestCase(MainMenuConfirmationKind.OverwriteSlot, "슬롯 덮어쓰기", "2번 슬롯에서 새 게임을 시작할까요?", "기존 진행 상황은 덮어씌워집니다.", "새 게임")]
        [TestCase(MainMenuConfirmationKind.QuitGame, "게임 종료", "데스크톱으로 종료할까요?", "저장되지 않은 진행 상황은 사라질 수 있습니다.", "종료")]
        public void ConfirmationPresenter_ResolvesKoreanCopy(
            MainMenuConfirmationKind kind,
            string title,
            string body,
            string warning,
            string confirm)
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault("ko-KR");
            using var presenter = new ConfirmPopupPresenter(resolver);
            presenter.Apply(MainMenuLocalization.CreateConfirmationPayload(
                kind,
                kind == MainMenuConfirmationKind.QuitGame ? null : 2));

            Assert.That(presenter.ViewModel.TitleText, Is.EqualTo(title));
            Assert.That(presenter.ViewModel.BodyText, Is.EqualTo(body));
            Assert.That(presenter.ViewModel.WarningText, Is.EqualTo(warning));
            Assert.That(presenter.ViewModel.ConfirmLabel, Is.EqualTo(confirm));
            Assert.That(presenter.ViewModel.CancelLabel, Is.EqualTo("취소"));
        }
    }

    public sealed class MainMenuLocaleLifecycleTests
    {
        [Test]
        public void VisibleSlotCardsRefreshOnLocaleChangeAndStopAfterControllerDispose()
        {
            var resolver = new TrackingResolver();
            var store = new InMemorySaveStore(new[]
            {
                new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-0-1"),
                    CurrentLevelGroupId = "level-0",
                    RemainingChances = 3,
                    TotalDeaths = 1,
                    LastPlayedAt = "2026-07-29T12:34:00+09:00",
                },
                SaveSlotData.CreateEmpty(2),
                SaveSlotData.CreateEmpty(3),
            });
            var controller = new MainMenuController(
                store,
                new NoOpHandoffStore(),
                new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance()),
                new NoOpLaunchRouter(),
                new RecordingConfirmPort(),
                localizedTextResolver: resolver);
            SaveSlotPanelViewModel refreshed = null;
            var refreshCount = 0;
            controller.ViewModelChanged += viewModel =>
            {
                refreshed = viewModel;
                refreshCount++;
            };

            Assert.That(controller.BuildViewModel().SlotCards[0].TitleText, Is.EqualTo("Slot 1"));
            Assert.That(resolver.SubscriberCount, Is.EqualTo(1));

            resolver.SetLocale("ko-KR");
            Assert.That(refreshCount, Is.EqualTo(1));
            Assert.That(refreshed.SlotCards[0].TitleText, Is.EqualTo("슬롯 1"));
            Assert.That(refreshed.SlotCards[0].StageText, Is.EqualTo("스테이지 Lab-01"));

            resolver.SetLocale("en-US");
            Assert.That(refreshCount, Is.EqualTo(2));
            Assert.That(refreshed.SlotCards[0].TitleText, Is.EqualTo("Slot 1"));

            controller.Dispose();
            Assert.That(resolver.SubscriberCount, Is.Zero);
            resolver.SetLocale("ko-KR");
            Assert.That(refreshCount, Is.EqualTo(2));
        }

        [Test]
        public void VisibleErrorCardsRefreshEnKoEnAndStopAfterControllerDispose()
        {
            var resolver = new TrackingResolver();
            const string diagnostic =
                "IOException: C:\\Users\\Player\\Saves\\profile.json is locked.";
            var store = new InMemorySaveStore(
                new[]
                {
                    SaveSlotData.CreateEmpty(1),
                    SaveSlotData.CreateEmpty(2),
                    SaveSlotData.CreateEmpty(3),
                },
                new CampaignSaveLoadReport(
                    CampaignSaveLoadStatus.IoFailed,
                    diagnostic,
                    "CampaignProfileDocument"));
            var controller = new MainMenuController(
                store,
                new NoOpHandoffStore(),
                new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance()),
                new NoOpLaunchRouter(),
                new RecordingConfirmPort(),
                localizedTextResolver: resolver);
            SaveSlotPanelViewModel refreshed = null;
            var refreshCount = 0;
            controller.ViewModelChanged += viewModel =>
            {
                refreshed = viewModel;
                refreshCount++;
            };

            var english = controller.BuildViewModel().SlotCards[0];
            Assert.That(english.TitleText, Is.EqualTo("Slot 1"));
            Assert.That(english.StatusText, Is.EqualTo("Save Load Failed"));
            Assert.That(english.StageText, Is.EqualTo("The save data could not be loaded."));
            Assert.That(english.StageText, Does.Not.Contain(diagnostic));
            Assert.That(english.PrimaryIntentKind, Is.EqualTo(SaveSlotIntentKind.None));
            Assert.That(english.ShowDelete, Is.False);
            Assert.That(resolver.SubscriberCount, Is.EqualTo(1));

            resolver.SetLocale("ko-KR");
            Assert.That(refreshCount, Is.EqualTo(1));
            Assert.That(refreshed.SlotCards[0].TitleText, Is.EqualTo("슬롯 1"));
            Assert.That(refreshed.SlotCards[0].StatusText, Is.EqualTo("저장 불러오기 실패"));
            Assert.That(refreshed.SlotCards[0].StageText, Is.EqualTo("저장 데이터를 불러올 수 없습니다."));
            Assert.That(refreshed.SlotCards[0].PrimaryIntentKind, Is.EqualTo(SaveSlotIntentKind.None));
            Assert.That(refreshed.SlotCards[0].ShowDelete, Is.False);

            resolver.SetLocale("en-US");
            Assert.That(refreshCount, Is.EqualTo(2));
            Assert.That(refreshed.SlotCards[0].StatusText, Is.EqualTo("Save Load Failed"));
            Assert.That(refreshed.SlotCards[0].StageText, Is.EqualTo("The save data could not be loaded."));

            controller.Dispose();
            Assert.That(resolver.SubscriberCount, Is.Zero);
            resolver.SetLocale("ko-KR");
            Assert.That(refreshCount, Is.EqualTo(2));
        }

        [Test]
        public void OpenConfirmationRefreshesAllCopyWithoutReplacingPayloadAndStopsAfterDispose()
        {
            var resolver = new TrackingResolver();
            var presenter = new ConfirmPopupPresenter(resolver);
            var payload = MainMenuLocalization.CreateConfirmationPayload(
                MainMenuConfirmationKind.RestartSlot,
                3);
            presenter.Apply(payload);
            Assert.That(resolver.SubscriberCount, Is.EqualTo(1));

            resolver.SetLocale("ko-KR");
            Assert.That(presenter.ViewModel.TitleText, Is.EqualTo("슬롯 다시 시작"));
            Assert.That(presenter.ViewModel.BodyText, Is.EqualTo("3번 슬롯을 처음부터 다시 시작할까요?"));
            Assert.That(presenter.ViewModel.WarningText, Is.EqualTo("기존 진행 상황은 교체됩니다."));

            resolver.SetLocale("en-US");
            Assert.That(presenter.ViewModel.TitleText, Is.EqualTo("Restart Slot"));
            Assert.That(presenter.ViewModel.BodyText, Is.EqualTo("Restart slot 3 from the beginning?"));

            presenter.Dispose();
            Assert.That(resolver.SubscriberCount, Is.Zero);
            resolver.SetLocale("ko-KR");
            Assert.That(presenter.ViewModel.TitleText, Is.EqualTo("Restart Slot"));
        }
    }

    public sealed class MainMenuLocalizationArchitectureTests
    {
        [Test]
        public void ProductionControllersAndNormalSlotMappingOwnNoM1BRawCopy()
        {
            var controller = File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");
            var hub = File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuHubController.cs");
            var mapper = File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuSlotViewModelMapper.cs");
            var view = File.ReadAllText(
                "Assets/_Features/UI/UI_Screens/Runtime/SaveSlotCardView.cs");
            var domain = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/ICampaignSaveSlotStore.cs");

            foreach (var forbidden in new[]
                     {
                         "\"Delete Slot\"",
                         "\"Restart Slot\"",
                         "\"Overwrite Slot\"",
                         "\"Quit Game\"",
                         "\"Quit to desktop?\"",
                     })
            {
                Assert.That(controller + hub, Does.Not.Contain(forbidden), forbidden);
            }

            foreach (var forbidden in new[]
                     {
                         "$\"Slot {",
                         "\"Empty\"",
                         "\"Completed\"",
                         "\"New Game\"",
                         "$\"Chances {",
                         "$\"Played {",
                     })
            {
                Assert.That(mapper, Does.Not.Contain(forbidden), forbidden);
            }

            Assert.That(mapper, Does.Contain("StageDisplayNameTextDescriptors.ForStage"));
            Assert.That(mapper, Does.Not.Contain("sequenceResolver.GetDisplayName"));
            Assert.That(mapper, Does.Not.Contain("report.Reason"));
            Assert.That(mapper, Does.Not.Contain("Reason.Contains"));
            Assert.That(mapper, Does.Not.Contain("Reason.StartsWith"));
            Assert.That(domain, Does.Not.Contain("ui.main_menu"));
            Assert.That(view, Does.Not.Contain("ko-KR"));
            Assert.That(view, Does.Not.Contain("en-US"));
            Assert.That(controller, Does.Contain("_localizedTextResolver.LocaleChanged += HandleLocaleChanged"));
            Assert.That(controller, Does.Contain("_localizedTextResolver.LocaleChanged -= HandleLocaleChanged"));
        }
    }

    internal sealed class TrackingResolver : ILocalizedTextResolver
    {
        private readonly PackageFreeLocalizedTextResolver _inner =
            PackageFreeLocalizedTextResolver.CreateSettingsDefault();
        private Action _localeChanged;

        public string CurrentLocaleCode => _inner.CurrentLocaleCode;

        public int SubscriberCount => _localeChanged?.GetInvocationList().Length ?? 0;

        public event Action LocaleChanged
        {
            add => _localeChanged += value;
            remove => _localeChanged -= value;
        }

        public string Resolve(LocalizedTextDescriptor descriptor)
        {
            return _inner.Resolve(descriptor);
        }

        public void SetLocale(string localeCode)
        {
            _inner.SetLocale(localeCode);
            _localeChanged?.Invoke();
        }
    }

    internal sealed class InMemorySaveStore : ICampaignSaveSlotStore
    {
        private readonly SaveSlotData[] _slots;
        private readonly CampaignSaveLoadReport _report;

        public InMemorySaveStore(
            SaveSlotData[] slots,
            CampaignSaveLoadReport? report = null)
        {
            _slots = slots;
            _report = report ?? CampaignSaveLoadReport.Loaded("test", string.Empty);
        }

        public string DiagnosticsKey => "m1b-test";

        public CampaignSaveLoadReport LastCampaignLoadReport => _report;

        public SaveSlotData[] LoadAll() => _slots.Select(slot => slot.Clone()).ToArray();

        public CampaignSaveLoadResult LoadAllWithReport() =>
            new CampaignSaveLoadResult(LoadAll(), LastCampaignLoadReport);

        public SaveSlotData LoadSlot(int slotNumber) =>
            _slots.First(slot => slot.SlotNumber == slotNumber).Clone();

        public void SaveSlot(SaveSlotData slot)
        {
        }

        public SaveSlotData InitializeNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt) => SaveSlotData.CreateNewGame(slotNumber, sequenceResolver, lastPlayedAt);

        public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation)
        {
        }

        public void DeleteSlot(int slotNumber)
        {
        }

        public void ClearAll()
        {
        }
    }

    internal sealed class NoOpHandoffStore : ICampaignLaunchHandoffStore
    {
        public bool TryBegin(
            int slotNumber,
            StageId stageId,
            StageNavigationKind navigationKind,
            string source,
            out CampaignLaunchHandoff handoff)
        {
            handoff = null;
            return false;
        }

        public bool TryPeek(out CampaignLaunchHandoff handoff)
        {
            handoff = null;
            return false;
        }

        public bool TryClear(Guid token) => true;

        public bool TryConsume(Guid token, out CampaignLaunchHandoff handoff)
        {
            handoff = null;
            return false;
        }
    }

    internal sealed class NoOpLaunchRouter : IStageLaunchRouter
    {
        public void Launch(StageNavigationRequest request)
        {
        }
    }

    internal sealed class RecordingConfirmPort : IConfirmPopupPort
    {
        public void Request(ConfirmPopupPayload payload, Action<bool> completion)
        {
        }
    }
}
