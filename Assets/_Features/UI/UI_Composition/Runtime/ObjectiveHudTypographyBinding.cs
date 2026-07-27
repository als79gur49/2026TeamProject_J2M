using System;
using System.Collections.Generic;
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
        private readonly Dictionary<TMP_Text, TmpTypographyAuthoredState> _authoredStates =
            new Dictionary<TMP_Text, TmpTypographyAuthoredState>();
        private readonly Dictionary<Material, Material> _runtimeUiMaterials =
            new Dictionary<Material, Material>();

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
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(this))
            {
                throw new InvalidOperationException(
                    $"{nameof(ObjectiveHudTypographyBinding)} cannot apply typography to a persistent asset. " +
                    "Instantiate the authored HUD first.");
            }
#endif

            var binding = TypographyBinding.FindFor(target);
            if (binding != null)
            {
                binding.Configure(target, styleTag);
            }

            var authoredState = binding != null
                ? binding.CaptureAuthoredState()
                : GetOrCaptureAuthoredState(target);
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

            var resolvedStyle = ResolveHudSafeStyle(
                _theme.ResolveOrThrow(_localeSource.CurrentLocaleCode, styleTag));
            const TypographyApplyMask requiredMask =
                TypographyApplyMask.Font |
                TypographyApplyMask.Material |
                TypographyApplyMask.FontStyle;
            if (binding != null)
            {
                LocalizedTmpTextApplicator.ApplyResolvedTypography(
                    target,
                    resolvedStyle,
                    binding,
                    requiredMask);
                return;
            }

            LocalizedTmpTextApplicator.ApplyResolvedTypography(
                target,
                resolvedStyle,
                authoredState,
                resolvedStyle.ApplyMask | requiredMask,
                TypographySizingSource.Hybrid);
        }

        private TmpTypographyAuthoredState GetOrCaptureAuthoredState(TMP_Text target)
        {
            if (!_authoredStates.TryGetValue(target, out var authoredState))
            {
                authoredState = TmpTypographyAuthoredState.Capture(target);
                _authoredStates.Add(target, authoredState);
            }

            return authoredState;
        }

        private ResolvedTmpTypographyStyle ResolveHudSafeStyle(
            ResolvedTmpTypographyStyle style)
        {
            var sourceMaterial = style.MaterialPreset;
            if (sourceMaterial == null ||
                sourceMaterial.shader == null ||
                !string.Equals(
                    sourceMaterial.shader.name,
                    "TextMeshPro/Mobile/Distance Field",
                    StringComparison.Ordinal))
            {
                return style;
            }

            if (!_runtimeUiMaterials.TryGetValue(sourceMaterial, out var runtimeMaterial) ||
                runtimeMaterial == null)
            {
                var uiShader = Shader.Find("TextMeshPro/Distance Field");
                if (uiShader == null)
                {
                    throw new InvalidOperationException(
                        "Objective HUD could not resolve the TextMeshPro UI distance-field shader.");
                }

                runtimeMaterial = new Material(uiShader)
                {
                    name = $"{sourceMaterial.name} (ObjectiveHud Runtime)",
                };
                runtimeMaterial.CopyPropertiesFromMaterial(sourceMaterial);
                runtimeMaterial.shader = uiShader;
                _runtimeUiMaterials[sourceMaterial] = runtimeMaterial;
            }

            return new ResolvedTmpTypographyStyle(
                style.FontAsset,
                runtimeMaterial,
                style.FontStyle,
                style.SizingSource,
                style.SizingMode,
                style.FixedSize,
                style.MinSize,
                style.MaxSize,
                style.LineSpacing,
                style.CharacterSpacing,
                style.ApplyMask,
                style.WeightStrategy);
        }

        private void OnDestroy()
        {
            foreach (var material in _runtimeUiMaterials.Values)
            {
                if (material == null)
                {
                    continue;
                }

                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }

            _runtimeUiMaterials.Clear();
        }
    }
}
