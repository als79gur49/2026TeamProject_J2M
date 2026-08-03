using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using Game.Feature.UI.Application;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class GameplayHudLocalizationBinding : MonoBehaviour, IDisposable
    {
        [SerializeField] private GameplayUiTypographyTheme _theme;
        [SerializeField] private TMP_Text _stageNameText;
        [SerializeField] private TMP_Text _pauseText;
        [SerializeField] private TMP_Text _chancesText;

        private TmpTypographyAuthoredState _stageNameAuthoredState;
        private TmpTypographyAuthoredState _pauseAuthoredState;
        private TmpTypographyAuthoredState _chancesAuthoredState;
        private ILocalizedTextResolver _textResolver;
        private bool _isDisposed;
        private bool _isInitialized;

        public GameplayUiTypographyTheme Theme => _theme;

        public TMP_Text StageNameText => _stageNameText;

        public TMP_Text PauseText => _pauseText;

        public TMP_Text ChancesText => _chancesText;

        public void Initialize(ILocalizedTextResolver textResolver)
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameplayHudLocalizationBinding)} is already initialized.");
            }

            ValidateAuthoredStructureOrThrow();
            _textResolver = textResolver ?? throw new ArgumentNullException(nameof(textResolver));
            _stageNameAuthoredState = TmpTypographyAuthoredState.Capture(_stageNameText);
            _pauseAuthoredState = TmpTypographyAuthoredState.Capture(_pauseText);
            _chancesAuthoredState = TmpTypographyAuthoredState.Capture(_chancesText);
            _textResolver.LocaleChanged += HandleLocaleChanged;
            _isDisposed = false;
            _isInitialized = true;
            Refresh();
        }

        public void Refresh()
        {
            if (!_isInitialized || _isDisposed)
            {
                return;
            }

            ApplyTypography(
                _stageNameText,
                _theme,
                _textResolver.CurrentLocaleCode,
                TypographyStyleTag.HeaderSmall,
                _stageNameAuthoredState);
            Apply(
                _pauseText,
                HudWorldGuideLocalization.PauseDescriptor,
                TypographyStyleTag.Button,
                _pauseAuthoredState);
            Apply(
                _chancesText,
                HudWorldGuideLocalization.ChancesDescriptor,
                TypographyStyleTag.HeaderSmall,
                _chancesAuthoredState);
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            if (_theme == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameplayHudLocalizationBinding)} is missing the production typography theme.");
            }

            if (_stageNameText == null || _pauseText == null || _chancesText == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameplayHudLocalizationBinding)} requires Stage name, Pause, and Chances TMP targets.");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_textResolver != null)
            {
                _textResolver.LocaleChanged -= HandleLocaleChanged;
            }

            _textResolver = null;
            _isDisposed = true;
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void HandleLocaleChanged()
        {
            Refresh();
        }

        private void Apply(
            TMP_Text target,
            LocalizedTextDescriptor descriptor,
            TypographyStyleTag styleTag,
            TmpTypographyAuthoredState authoredState)
        {
            target.text = _textResolver.Resolve(descriptor) ?? string.Empty;
            ApplyTypography(
                target,
                _theme,
                _textResolver.CurrentLocaleCode,
                styleTag,
                authoredState);
        }

        internal static void ApplyTypography(
            TMP_Text target,
            GameplayUiTypographyTheme theme,
            string localeCode,
            TypographyStyleTag styleTag,
            TmpTypographyAuthoredState authoredState)
        {
            if (target == null || theme == null)
            {
                return;
            }

            if (string.Equals(
                    localeCode,
                    UnityStringTableTextResolver.DefaultLocaleCode,
                    StringComparison.Ordinal))
            {
                target.font = authoredState.OriginalFont;
                target.fontSharedMaterial = authoredState.OriginalMaterial;
                target.fontStyle = authoredState.OriginalFontStyle;
                return;
            }

            var resolvedStyle = theme.ResolveOrThrow(localeCode, styleTag);
            const TypographyApplyMask requiredMask =
                TypographyApplyMask.Font |
                TypographyApplyMask.Material |
                TypographyApplyMask.FontStyle;
            LocalizedTmpTextApplicator.ApplyResolvedTypography(
                target,
                resolvedStyle,
                authoredState,
                resolvedStyle.ApplyMask | requiredMask,
                TypographySizingSource.Hybrid);
        }
    }

    public sealed class GameplayWorldGuideLocalizationController : IDisposable
    {
        private readonly Dictionary<TMP_Text, TmpTypographyAuthoredState> _authoredStates = new();
        private readonly IWorldGuideLocalizationSource _source;
        private readonly ILocalizedTextResolver _textResolver;
        private readonly GameplayUiTypographyTheme _theme;
        private readonly List<IWorldGuideLocalizationTarget> _views = new();
        private bool _isDisposed;

        public GameplayWorldGuideLocalizationController(
            IWorldGuideLocalizationSource source,
            ILocalizedTextResolver textResolver,
            GameplayUiTypographyTheme theme)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _textResolver = textResolver ?? throw new ArgumentNullException(nameof(textResolver));
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _source.GuidesChanged += HandleGuidesChanged;
            _textResolver.LocaleChanged += HandleLocaleChanged;
            Refresh();
        }

        public void Refresh()
        {
            if (_isDisposed)
            {
                return;
            }

            _source.CopyLocalizationTargets(_views);
            RemoveStaleAuthoredStates();
            for (var i = 0; i < _views.Count; i++)
            {
                Apply(_views[i]);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _source.GuidesChanged -= HandleGuidesChanged;
            _textResolver.LocaleChanged -= HandleLocaleChanged;
            _views.Clear();
            _authoredStates.Clear();
            _isDisposed = true;
        }

        public static bool TryMapInstructionKind(
            WorldGuideInstructionKind instructionKind,
            out WorldGuideActionLocalizationKind localizationKind)
        {
            switch (instructionKind)
            {
                case WorldGuideInstructionKind.Movement:
                    localizationKind = WorldGuideActionLocalizationKind.Movement;
                    return true;
                case WorldGuideInstructionKind.Push:
                    localizationKind = WorldGuideActionLocalizationKind.Push;
                    return true;
                case WorldGuideInstructionKind.Flip:
                    localizationKind = WorldGuideActionLocalizationKind.Flip;
                    return true;
                case WorldGuideInstructionKind.None:
                default:
                    localizationKind = WorldGuideActionLocalizationKind.None;
                    return false;
            }
        }

        private void Apply(IWorldGuideLocalizationTarget view)
        {
            if (view == null ||
                !TryMapInstructionKind(view.InstructionKind, out var localizationKind) ||
                !HudWorldGuideLocalization.TryCreateWorldGuideDescriptor(
                    localizationKind,
                    out var descriptor))
            {
                view?.ApplyActionText(string.Empty);
                return;
            }

            var target = view.ActionTextLabel;
            view.ApplyActionText(_textResolver.Resolve(descriptor) ?? string.Empty);
            if (target == null)
            {
                return;
            }

            if (!_authoredStates.TryGetValue(target, out var authoredState))
            {
                authoredState = TmpTypographyAuthoredState.Capture(target);
                _authoredStates.Add(target, authoredState);
            }

            GameplayHudLocalizationBinding.ApplyTypography(
                target,
                _theme,
                _textResolver.CurrentLocaleCode,
                TypographyStyleTag.Body,
                authoredState);
        }

        private void RemoveStaleAuthoredStates()
        {
            var stale = new List<TMP_Text>();
            foreach (var pair in _authoredStates)
            {
                if (pair.Key == null || !_views.Exists(view => view != null && view.ActionTextLabel == pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                _authoredStates.Remove(stale[i]);
            }
        }

        private void HandleGuidesChanged()
        {
            Refresh();
        }

        private void HandleLocaleChanged()
        {
            Refresh();
        }
    }
}
