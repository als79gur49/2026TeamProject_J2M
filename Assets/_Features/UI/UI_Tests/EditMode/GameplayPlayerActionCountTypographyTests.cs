using System;
using System.Linq;
using Game.Feature.Gameplay.Host;
using Game.Feature.UI.Composition;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayPlayerActionCountTypographyTests
    {
        private const string PrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Prefabs/PlayerActionCountView.prefab";
        private const string ThemePath =
            "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset";

        [Test]
        public void ActiveCounter_UsesValueFontAndMaterialForEachLocaleWithoutChangingText()
        {
            var view = InstantiateView();
            try
            {
                var source = new CounterViewSource(view);
                var locale = new LocaleSource();
                var theme = LoadTheme();
                view.Hide();

                using var controller = new GameplayPlayerActionCountTypographyController(
                    source, locale, theme);
                view.ShowCount(12);
                foreach (var localeCode in new[] { "en-US", "ko-KR", "ja-JP", "zh-CN" })
                {
                    locale.SetLocale(localeCode);
                    var resolved = theme.ResolveOrThrow(localeCode, TypographyStyleTag.Value);
                    Assert.That(view.CountLabel.font, Is.SameAs(resolved.FontAsset), localeCode);
                    Assert.That(view.CountLabel.fontSharedMaterial, Is.SameAs(resolved.MaterialPreset), localeCode);
                    Assert.That(view.CountLabel.text, Is.EqualTo("x12"), localeCode);
                    Assert.That(resolved.FontAsset.characterTable.Any(character => character.unicode == 'x'),
                        Is.True, localeCode);
                    for (var digit = '0'; digit <= '9'; digit++)
                    {
                        Assert.That(resolved.FontAsset.characterTable.Any(character => character.unicode == digit),
                            Is.True, $"{localeCode}: {digit}");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void ReplacedAndRemovedCounter_ReleasesOldLocaleBinding()
        {
            var first = InstantiateView();
            var second = InstantiateView();
            try
            {
                var source = new CounterViewSource(first);
                var locale = new LocaleSource();
                var theme = LoadTheme();
                using var controller = new GameplayPlayerActionCountTypographyController(
                    source, locale, theme);

                var englishFont = first.CountLabel.font;
                source.SetView(second);
                locale.SetLocale("ko-KR");
                Assert.That(first.CountLabel.font, Is.SameAs(englishFont));
                Assert.That(second.CountLabel.font,
                    Is.SameAs(theme.ResolveOrThrow("ko-KR", TypographyStyleTag.Value).FontAsset));

                var koreanFont = second.CountLabel.font;
                source.SetView(null);
                locale.SetLocale("ja-JP");
                Assert.That(second.CountLabel.font, Is.SameAs(koreanFont));

                source.SetView(first);
                Assert.That(first.CountLabel.font,
                    Is.SameAs(theme.ResolveOrThrow("ja-JP", TypographyStyleTag.Value).FontAsset));
                controller.Dispose();
                locale.SetLocale("zh-CN");
                Assert.That(first.CountLabel.font,
                    Is.SameAs(theme.ResolveOrThrow("ja-JP", TypographyStyleTag.Value).FontAsset));
            }
            finally
            {
                Object.DestroyImmediate(first.gameObject);
                Object.DestroyImmediate(second.gameObject);
            }
        }

        private static GameplayPlayerActionCountView InstantiateView()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            var view = instance.GetComponent<GameplayPlayerActionCountView>();
            Assert.That(view, Is.Not.Null);
            return view;
        }

        private static GameplayUiTypographyTheme LoadTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(ThemePath);
            Assert.That(theme, Is.Not.Null);
            return theme;
        }

        private sealed class CounterViewSource : IGameplayPlayerActionCountViewSource
        {
            public CounterViewSource(GameplayPlayerActionCountView view)
            {
                CurrentCounterView = view;
            }

            public GameplayPlayerActionCountView CurrentCounterView { get; private set; }

            public event Action<GameplayPlayerActionCountView> CounterViewChanged;

            public void SetView(GameplayPlayerActionCountView view)
            {
                CurrentCounterView = view;
                CounterViewChanged?.Invoke(view);
            }
        }

        private sealed class LocaleSource : ILocalizedTextResolver
        {
            public string CurrentLocaleCode { get; private set; } = "en-US";

            public event Action LocaleChanged;

            public string Resolve(LocalizedTextDescriptor descriptor) => string.Empty;

            public void SetLocale(string localeCode)
            {
                CurrentLocaleCode = localeCode;
                LocaleChanged?.Invoke();
            }
        }
    }
}
