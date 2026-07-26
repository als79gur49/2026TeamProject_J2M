using System;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class ObjectiveHudTypographyBinding : MonoBehaviour, IObjectiveHudTypographyApplicator
    {
        [SerializeField] private GameplayUiTypographyTheme _theme;
        [SerializeField] private TMP_Text _headerTarget;
        [SerializeField] private TypographyStyleTag _headerStyle = TypographyStyleTag.HeaderSmall;
        [SerializeField] private TypographyStyleTag _rowStyle = TypographyStyleTag.BodySmall;

        private ILocalizedTextResolver _localeSource;

        public GameplayUiTypographyTheme Theme => _theme;

        public TypographyStyleTag HeaderStyle => _headerStyle;

        public TypographyStyleTag RowStyle => _rowStyle;

        public void Initialize(ILocalizedTextResolver localeSource)
        {
            _localeSource = localeSource ?? throw new ArgumentNullException(nameof(localeSource));
            ApplyHeader(_headerTarget);
        }

        public void ApplyHeader(TMP_Text target)
        {
            Apply(target != null ? target : _headerTarget, _headerStyle);
        }

        public void ApplyRow(TMP_Text target)
        {
            Apply(target, _rowStyle);
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            if (_theme == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(ObjectiveHudTypographyBinding)} is missing the production typography theme.");
            }

            if (_headerTarget == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(ObjectiveHudTypographyBinding)} is missing the Objectives header target.");
            }
        }

        private void Apply(TMP_Text target, TypographyStyleTag styleTag)
        {
            if (target == null || _localeSource == null)
            {
                return;
            }

            ValidateAuthoredStructureOrThrow();
            var binding = TypographyBinding.FindFor(target);
            if (binding == null)
            {
                binding = target.gameObject.AddComponent<TypographyBinding>();
            }

            binding.Configure(target, styleTag);
            var authoredState = binding.CaptureAuthoredState();
            if (string.Equals(
                    _localeSource.CurrentLocaleCode,
                    UnityStringTableTextResolver.DefaultLocaleCode,
                    StringComparison.Ordinal))
            {
                target.font = authoredState.OriginalFont;
                target.fontSharedMaterial = authoredState.OriginalMaterial;
                target.fontStyle = authoredState.OriginalFontStyle;
                return;
            }

            var resolvedStyle = _theme.ResolveOrThrow(_localeSource.CurrentLocaleCode, styleTag);
            LocalizedTmpTextApplicator.ApplyResolvedTypography(
                target,
                resolvedStyle,
                binding,
                TypographyApplyMask.Font |
                TypographyApplyMask.Material |
                TypographyApplyMask.FontStyle);
        }
    }
}
