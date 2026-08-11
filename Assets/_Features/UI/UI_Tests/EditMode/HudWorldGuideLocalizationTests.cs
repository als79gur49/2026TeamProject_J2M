using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Host;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.HUD;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace Game.Feature.UI.Tests
{
    public sealed class HudWorldGuideLocalizationTests
    {
        private const string HudPrefabPath =
            "Assets/_Features/UI/UI_HUD/Prefabs/GameplayHudRoot.prefab";
        private const string ThemePath =
            "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset";
        private const string ClimatePath =
            "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2019 SDF.asset";

        [Test]
        public void Contract_HasThreeUniqueNonBlankWorldGuideLocaleEntries()
        {
            var entries = HudWorldGuideLocalization.Entries;

            Assert.That(entries, Has.Count.EqualTo(3));
            Assert.That(entries.Select(entry => entry.Id).Distinct().Count(), Is.EqualTo(3));
            Assert.That(entries.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(3));
            Assert.That(entries.All(entry => !string.IsNullOrWhiteSpace(entry.English)), Is.True);
            Assert.That(entries.All(entry => !string.IsNullOrWhiteSpace(entry.Korean)), Is.True);
            Assert.That(
                entries.Select(entry => (entry.Key, entry.English, entry.Korean)),
                Is.EquivalentTo(new[]
                {
                    (HudWorldGuideLocalization.Keys.Movement, "Move", "이동"),
                    (HudWorldGuideLocalization.Keys.Push, "Push", "밀기"),
                    (HudWorldGuideLocalization.Keys.Flip, "Flip", "뒤집기"),
                }));
        }

        [Test]
        public void UnityStringTables_MatchHudWorldGuideContractWithoutPlaceholders()
        {
            var english = LoadTable("Assets/Localization/StringTables/UI/UI_en-US.asset");
            var korean = LoadTable("Assets/Localization/StringTables/UI/UI_ko-KR.asset");

            foreach (var entry in HudWorldGuideLocalization.Entries)
            {
                Assert.That(english.GetEntry(entry.Key)?.LocalizedValue, Is.EqualTo(entry.English), entry.Key);
                Assert.That(korean.GetEntry(entry.Key)?.LocalizedValue, Is.EqualTo(entry.Korean), entry.Key);
                Assert.That(CountPlaceholders(entry.English), Is.EqualTo(CountPlaceholders(entry.Korean)), entry.Key);
            }

            foreach (var retiredKey in new[] { "ui.hud.pause", "ui.hud.chances", "ui.pause.description" })
            {
                Assert.That(english.GetEntry(retiredKey), Is.Null, retiredKey);
                Assert.That(korean.GetEntry(retiredKey), Is.Null, retiredKey);
            }
        }

        [Test]
        public void GameplayHudPrefab_DoesNotExposeRetiredPauseOrChanceCopy()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            var authoredText = prefab.GetComponentsInChildren<TMP_Text>(true)
                .Select(text => text.text)
                .ToArray();

            Assert.That(authoredText, Does.Not.Contain("Pause"));
            Assert.That(authoredText, Does.Not.Contain("CHANCES"));
            Assert.That(
                typeof(GameplayHudLocalizationBinding).GetField("_pauseText", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(GameplayHudLocalizationBinding).GetField("_chancesText", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(ChancePanelView).GetField("_labelText", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
        }

        [Test]
        public void GameplayHudBinding_LocaleRoundTripKeepsChanceStateAndDisposesSubscription()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            var instance = Object.Instantiate(prefab);
            var resolver = new ContractResolver("en-US");
            var climate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ClimatePath);

            try
            {
                var binding = instance.GetComponent<GameplayHudLocalizationBinding>();
                var chanceView = instance.GetComponent<HUDRootView>().ChancePanelView;
                var chanceModel = new ChancePanelViewModel();
                Assert.That(
                    binding.StageNameText.rectTransform.offsetMax.x,
                    Is.EqualTo(-97.1592f).Within(0.01f),
                    "Stage name must use the expanded authored single-line lane.");
                Assert.That(
                    binding.StageNameText.textWrappingMode,
                    Is.EqualTo(TextWrappingModes.NoWrap));
                Assert.That(binding.StageNameText.enableAutoSizing, Is.True);
                Assert.That(binding.StageNameText.fontSizeMin, Is.EqualTo(14f));
                Assert.That(binding.StageNameText.fontSizeMax, Is.EqualTo(30f));
                Assert.That(
                    binding.StageNameText.overflowMode,
                    Is.EqualTo(TextOverflowModes.Overflow),
                    "Stage names must fit without ellipsis.");
                chanceView.Bind(chanceModel);
                chanceModel.SetState(
                    hasChances: true,
                    remainingChances: 2,
                    maxChances: 3,
                    isInitialBind: true,
                    ChanceChangeAnimationHint.None);
                binding.StageNameText.text = "Lab-01";
                var englishStageNameFont = binding.StageNameText.font;
                var englishStageNameMaterial = binding.StageNameText.fontSharedMaterial;
                binding.Initialize(resolver);
                Assert.That(resolver.SubscriberCount, Is.EqualTo(1));
                Assert.That(binding.StageNameText.text, Is.EqualTo("Lab-01"));
                AssertChanceState(chanceModel, 2, 3);

                instance.SetActive(false);
                binding.StageNameText.text = "연구실-01";
                resolver.SetLocale("ko-KR");
                instance.SetActive(true);

                Assert.That(binding.StageNameText.text, Is.EqualTo("연구실-01"));
                AssertChanceState(chanceModel, 2, 3);
                Assert.That(binding.StageNameText.font, Is.SameAs(climate));

                binding.StageNameText.text = "Lab-01";
                resolver.SetLocale("en-US");

                Assert.That(binding.StageNameText.text, Is.EqualTo("Lab-01"));
                Assert.That(binding.StageNameText.font, Is.SameAs(englishStageNameFont));
                Assert.That(
                    binding.StageNameText.fontSharedMaterial,
                    Is.SameAs(englishStageNameMaterial));
                AssertChanceState(chanceModel, 2, 3);

                binding.Dispose();
                Assert.That(resolver.SubscriberCount, Is.Zero);
                binding.StageNameText.text = "연구실-01";
                resolver.SetLocale("ko-KR");
                Assert.That(binding.StageNameText.text, Is.EqualTo("연구실-01"));
                Assert.That(binding.StageNameText.font, Is.SameAs(englishStageNameFont));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void WorldGuideController_LocalizesActionsAndLeavesKeycapsInvariant()
        {
            var resolver = new ContractResolver("en-US");
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(ThemePath);
            var climate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ClimatePath);
            var movement = new LocalizationTarget(WorldGuideInstructionKind.Movement, "WASD");
            var push = new LocalizationTarget(WorldGuideInstructionKind.Push, "J");
            var flip = new LocalizationTarget(WorldGuideInstructionKind.Flip, "K");
            var source = new LocalizationSource(movement, push, flip);
            GameplayWorldGuideLocalizationController controller = null;

            try
            {
                controller = new GameplayWorldGuideLocalizationController(
                    source,
                    resolver,
                    theme);
                var views = new List<IWorldGuideLocalizationTarget>();
                source.CopyLocalizationTargets(views);
                Assert.That(views, Has.Count.EqualTo(3));
                Assert.That(resolver.SubscriberCount, Is.EqualTo(1));

                var movementEnglishFont = movement.ActionTextLabel.font;
                var pushEnglishFont = push.ActionTextLabel.font;
                var flipEnglishFont = flip.ActionTextLabel.font;

                Assert.That(movement.ActionTextLabel.text, Is.EqualTo("Move"));
                Assert.That(push.ActionTextLabel.text, Is.EqualTo("Push"));
                Assert.That(flip.ActionTextLabel.text, Is.EqualTo("Flip"));
                Assert.That(movement.Keycap, Is.EqualTo("WASD"));
                Assert.That(push.Keycap, Is.EqualTo("J"));
                Assert.That(flip.Keycap, Is.EqualTo("K"));

                resolver.SetLocale("ko-KR");

                Assert.That(movement.ActionTextLabel.text, Is.EqualTo("이동"));
                Assert.That(push.ActionTextLabel.text, Is.EqualTo("밀기"));
                Assert.That(flip.ActionTextLabel.text, Is.EqualTo("뒤집기"));
                Assert.That(movement.Keycap, Is.EqualTo("WASD"));
                Assert.That(push.Keycap, Is.EqualTo("J"));
                Assert.That(flip.Keycap, Is.EqualTo("K"));
                Assert.That(movement.ActionTextLabel.font, Is.SameAs(climate));
                Assert.That(push.ActionTextLabel.font, Is.SameAs(climate));
                Assert.That(flip.ActionTextLabel.font, Is.SameAs(climate));

                resolver.SetLocale("en-US");

                Assert.That(movement.ActionTextLabel.text, Is.EqualTo("Move"));
                Assert.That(push.ActionTextLabel.text, Is.EqualTo("Push"));
                Assert.That(flip.ActionTextLabel.text, Is.EqualTo("Flip"));
                Assert.That(movement.ActionTextLabel.font, Is.SameAs(movementEnglishFont));
                Assert.That(push.ActionTextLabel.font, Is.SameAs(pushEnglishFont));
                Assert.That(flip.ActionTextLabel.font, Is.SameAs(flipEnglishFont));

                controller.Dispose();
                Assert.That(resolver.SubscriberCount, Is.Zero);
                resolver.SetLocale("ko-KR");
                Assert.That(movement.ActionTextLabel.text, Is.EqualTo("Move"));
            }
            finally
            {
                controller?.Dispose();
                movement.Dispose();
                push.Dispose();
                flip.Dispose();
            }
        }

        [Test]
        public void WorldGuideMapping_RejectsUnsupportedKind()
        {
            Assert.That(
                GameplayWorldGuideLocalizationController.TryMapInstructionKind(
                    WorldGuideInstructionKind.Movement,
                    out var movement),
                Is.True);
            Assert.That(movement, Is.EqualTo(WorldGuideActionLocalizationKind.Movement));
            Assert.That(
                GameplayWorldGuideLocalizationController.TryMapInstructionKind(
                    (WorldGuideInstructionKind)999,
                    out var unsupported),
                Is.False);
            Assert.That(unsupported, Is.EqualTo(WorldGuideActionLocalizationKind.None));
            Assert.That(
                HudWorldGuideLocalization.TryCreateWorldGuideDescriptor(
                    unsupported,
                    out _),
                Is.False);
        }

        private static StringTable LoadTable(string path)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>(path);
            Assert.That(table, Is.Not.Null, path);
            return table;
        }

        private static int CountPlaceholders(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : value.Count(character => character == '{');
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static void AssertChanceState(ChancePanelViewModel viewModel, int remaining, int maximum)
        {
            Assert.That(viewModel.RemainingChances, Is.EqualTo(remaining));
            Assert.That(viewModel.MaxChances, Is.EqualTo(maximum));
            Assert.That(viewModel.Slots.Count, Is.EqualTo(maximum));
            Assert.That(viewModel.Slots.Count(slot => slot.IsFilled), Is.EqualTo(remaining));
        }

        private sealed class LocalizationSource : IWorldGuideLocalizationSource
        {
            private readonly IWorldGuideLocalizationTarget[] _targets;

            public LocalizationSource(params IWorldGuideLocalizationTarget[] targets)
            {
                _targets = targets;
            }

            public event Action GuidesChanged;

            public void CopyLocalizationTargets(List<IWorldGuideLocalizationTarget> destination)
            {
                destination.Clear();
                destination.AddRange(_targets);
            }
        }

        private sealed class LocalizationTarget : IWorldGuideLocalizationTarget, IDisposable
        {
            private readonly GameObject _root;

            public LocalizationTarget(WorldGuideInstructionKind kind, string keycap)
            {
                InstructionKind = kind;
                Keycap = keycap;
                _root = new GameObject($"{kind}LocalizationTarget", typeof(RectTransform));
                WasdDisplayRoot = new GameObject("WASD");
                WasdDisplayRoot.transform.SetParent(_root.transform, false);
                ArrowDisplayRoot = new GameObject("Arrows");
                ArrowDisplayRoot.transform.SetParent(_root.transform, false);
                ArrowDisplayRoot.SetActive(false);
                var keyObject = new GameObject("Keycap", typeof(RectTransform), typeof(TextMeshProUGUI));
                keyObject.transform.SetParent(_root.transform, false);
                ActionKeyLabel = keyObject.GetComponent<TMP_Text>();
                ActionKeyLabel.text = keycap;
                var actionObject = new GameObject("Action", typeof(RectTransform), typeof(TextMeshProUGUI));
                actionObject.transform.SetParent(_root.transform, false);
                ActionTextLabel = actionObject.GetComponent<TMP_Text>();
            }

            public WorldGuideInstructionKind InstructionKind { get; }

            public string Keycap { get; }

            public GameObject WasdDisplayRoot { get; }

            public GameObject ArrowDisplayRoot { get; }

            public TMP_Text ActionKeyLabel { get; }

            public TMP_Text ActionTextLabel { get; }

            public void ApplyActionText(string resolvedActionText)
            {
                ActionTextLabel.text = resolvedActionText;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_root);
            }
        }

        private sealed class ContractResolver : ILocalizedTextResolver
        {
            private Action _localeChanged;

            public ContractResolver(string localeCode)
            {
                CurrentLocaleCode = localeCode;
            }

            public string CurrentLocaleCode { get; private set; }

            public int SubscriberCount { get; private set; }

            public event Action LocaleChanged
            {
                add
                {
                    _localeChanged += value;
                    SubscriberCount++;
                }
                remove
                {
                    _localeChanged -= value;
                    SubscriberCount--;
                }
            }

            public string Resolve(LocalizedTextDescriptor descriptor)
            {
                var entry = HudWorldGuideLocalization.Entries.SingleOrDefault(
                    candidate => string.Equals(candidate.Key, descriptor.Key, StringComparison.Ordinal));
                return string.Equals(CurrentLocaleCode, "ko-KR", StringComparison.Ordinal)
                    ? entry.Korean
                    : entry.English;
            }

            public void SetLocale(string localeCode)
            {
                CurrentLocaleCode = localeCode;
                _localeChanged?.Invoke();
            }
        }
    }
}
