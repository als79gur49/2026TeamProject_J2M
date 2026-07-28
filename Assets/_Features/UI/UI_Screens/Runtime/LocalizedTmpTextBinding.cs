using System;
using Game.Feature.UI.Composition;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.Screens
{
    public sealed class LocalizedTmpTextBinding : IDisposable
    {
        private readonly TMP_Text _target;
        private readonly LocalizedTextDescriptor _descriptor;
        private readonly ILocalizedTextResolver _textResolver;
        private readonly ILocalizedTypographyResolver _typographyResolver;
        private readonly ILocalizedTmpFontResolver _fontResolver;
        private readonly GameplayUiTypographyTheme _typographyTheme;
        private readonly TypographyBinding _typographyBinding;
        private readonly TypographyApplyMask _requiredThemeApplyMask;
        private readonly TMP_FontAsset _defaultFontAsset;
        private readonly Material _defaultMaterialPreset;
        private bool _isDisposed;

        public LocalizedTmpTextBinding(
            TMP_Text target,
            LocalizedTextDescriptor descriptor,
            ILocalizedTextResolver textResolver,
            ILocalizedTypographyResolver typographyResolver,
            ILocalizedTmpFontResolver fontResolver = null,
            GameplayUiTypographyTheme typographyTheme = null,
            TypographyBinding typographyBinding = null,
            TypographyApplyMask requiredThemeApplyMask = TypographyApplyMask.None)
        {
            _target = target;
            _descriptor = descriptor;
            _textResolver = textResolver;
            _typographyResolver = typographyResolver ?? DefaultLocalizedTypographyResolver.Instance;
            _fontResolver = fontResolver;
            _typographyTheme = typographyTheme;
            _typographyBinding = typographyBinding ?? TypographyBinding.FindFor(target);
            _requiredThemeApplyMask = requiredThemeApplyMask;
            _defaultFontAsset = target != null ? target.font : null;
            _defaultMaterialPreset = target != null ? target.fontSharedMaterial : null;

            if (_textResolver != null)
            {
                _textResolver.LocaleChanged += HandleLocaleChanged;
            }

            Refresh();
        }

        public void Refresh()
        {
            if (_isDisposed || _target == null)
            {
                return;
            }

            _target.text = ResolveText();
            var localeCode = _textResolver != null ? _textResolver.CurrentLocaleCode : string.Empty;

            if (!LocalizedTmpTextApplicator.ApplyTypographyTheme(
                    _target,
                    _typographyTheme,
                    localeCode,
                    _typographyBinding,
                    _requiredThemeApplyMask))
            {
                LocalizedTmpTextApplicator.ApplyTypography(
                    _target,
                    _typographyResolver.Resolve(
                        localeCode,
                        _descriptor.Role,
                        _descriptor.Weight));
                if (_fontResolver != null)
                {
                    LocalizedTmpTextApplicator.ApplyFont(
                        _target,
                        _fontResolver.ResolveFont(
                            localeCode,
                            _descriptor.Role,
                            _descriptor.Weight),
                        _defaultFontAsset,
                        _defaultMaterialPreset);
                }
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

            _isDisposed = true;
        }

        private string ResolveText()
        {
            if (_textResolver == null)
            {
                return _descriptor.Key;
            }

            return _textResolver.Resolve(_descriptor) ?? string.Empty;
        }

        private void HandleLocaleChanged()
        {
            Refresh();
        }
    }

    public sealed class LocalizedTmpTypographyBinding : IDisposable
    {
        private readonly TMP_Text _target;
        private readonly ILocalizedTextResolver _localeSource;
        private readonly GameplayUiTypographyTheme _typographyTheme;
        private readonly TypographyBinding _typographyBinding;
        private bool _isDisposed;

        public LocalizedTmpTypographyBinding(
            TMP_Text target,
            ILocalizedTextResolver localeSource,
            GameplayUiTypographyTheme typographyTheme,
            TypographyBinding typographyBinding = null)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _localeSource = localeSource ?? throw new ArgumentNullException(nameof(localeSource));
            _typographyTheme = typographyTheme ?? throw new ArgumentNullException(nameof(typographyTheme));
            _typographyBinding = typographyBinding ?? TypographyBinding.FindFor(target);
            if (_typographyBinding == null)
            {
                throw new InvalidOperationException(
                    $"Localized TMP typography target '{target.name}' requires a semantic TypographyBinding.");
            }

            try
            {
                _localeSource.LocaleChanged += HandleLocaleChanged;
                Refresh();
            }
            catch
            {
                _localeSource.LocaleChanged -= HandleLocaleChanged;
                throw;
            }
        }

        public void Refresh()
        {
            if (_isDisposed || _target == null)
            {
                return;
            }

            if (!LocalizedTmpTextApplicator.ApplyTypographyTheme(
                    _target,
                    _typographyTheme,
                    _localeSource.CurrentLocaleCode,
                    _typographyBinding))
            {
                throw new InvalidOperationException(
                    $"Typography style '{_typographyBinding.StyleTag}' is not available for locale " +
                    $"'{_localeSource.CurrentLocaleCode}'.");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _localeSource.LocaleChanged -= HandleLocaleChanged;
            _isDisposed = true;
        }

        private void HandleLocaleChanged()
        {
            Refresh();
        }
    }

    internal static class TerminalScreenTypographyUtility
    {
        public static void Apply(
            TMP_Text target,
            string localeCode,
            GameplayUiTypographyTheme typographyTheme,
            TypographyStyleTag styleTag)
        {
            if (target == null || string.Equals(localeCode, "en-US", StringComparison.Ordinal))
            {
                return;
            }

            if (typographyTheme == null)
            {
                throw new ArgumentNullException(nameof(typographyTheme));
            }

            var style = typographyTheme.ResolveOrThrow(localeCode, styleTag);
            var authoredState = TmpTypographyAuthoredState.Capture(target);
            target.fontSharedMaterial = null;
            LocalizedTmpTextApplicator.ApplyResolvedTypography(
                target,
                style,
                authoredState,
                TypographyApplyMask.Font |
                TypographyApplyMask.Material,
                TypographySizingSource.Hybrid);
            LocalizedTmpTextApplicator.ApplyResolvedTypography(
                target,
                style,
                authoredState,
                TypographyApplyMask.FontStyle,
                TypographySizingSource.Hybrid);
        }
    }

    public static class LocalizedTmpTextApplicator
    {
        private static readonly int ScaleRatioAProperty = Shader.PropertyToID("_ScaleRatioA");
        private static readonly int ScaleRatioBProperty = Shader.PropertyToID("_ScaleRatioB");
        private static readonly int ScaleRatioCProperty = Shader.PropertyToID("_ScaleRatioC");

        public static void ApplyTypography(TMP_Text target, LocalizedTypographyStyle style)
        {
            if (target == null)
            {
                return;
            }

            target.fontSize = style.FontSize;
            target.lineSpacing = style.LineSpacing;
            target.fontStyle = style.Bold
                ? target.fontStyle | FontStyles.Bold
                : target.fontStyle & ~FontStyles.Bold;
        }

        public static void ApplyFont(
            TMP_Text target,
            LocalizedTmpFontStyle style,
            TMP_FontAsset fallbackFontAsset = null,
            Material fallbackMaterialPreset = null)
        {
            if (target == null)
            {
                return;
            }

            if (style.FontAsset != null)
            {
                target.font = style.FontAsset;
            }
            else if (fallbackFontAsset != null)
            {
                target.font = fallbackFontAsset;
            }

            if (style.MaterialPreset != null)
            {
                target.fontSharedMaterial = style.MaterialPreset;
            }
            else if (style.FontAsset == null && fallbackMaterialPreset != null)
            {
                target.fontSharedMaterial = fallbackMaterialPreset;
            }
        }

        public static bool ApplyTypographyTheme(
            TMP_Text target,
            GameplayUiTypographyTheme theme,
            string localeCode,
            TypographyBinding binding = null,
            TypographyApplyMask requiredApplyMask = TypographyApplyMask.None)
        {
            if (target == null)
            {
                return false;
            }

            binding ??= TypographyBinding.FindFor(target);
            if (binding == null)
            {
                return false;
            }

            if (binding.LocaleParticipation == TypographyLocaleParticipation.LocaleInvariant)
            {
                return true;
            }

            if (theme == null)
            {
                return false;
            }

            var styleTag = binding.StyleTag;
            if (!theme.TryResolve(localeCode, styleTag, out var style))
            {
                return false;
            }

            ApplyResolvedTypography(target, style, binding, requiredApplyMask);
            return true;
        }

        public static void ApplyResolvedTypography(
            TMP_Text target,
            ResolvedTmpTypographyStyle style,
            TypographyBinding binding,
            TypographyApplyMask requiredApplyMask = TypographyApplyMask.None)
        {
            if (target == null || binding == null)
            {
                return;
            }

            if (binding.LocaleParticipation == TypographyLocaleParticipation.LocaleInvariant)
            {
                return;
            }

            var authoredState = binding.CaptureAuthoredState();
            var applyMask = binding.UseApplyMaskOverride
                ? binding.ApplyMaskOverride
                : style.ApplyMask;
            applyMask |= requiredApplyMask;
            ApplyResolvedTypography(
                target,
                style,
                authoredState,
                applyMask,
                binding.SizingSourceOverride);
        }

        public static void ApplyResolvedTypography(
            TMP_Text target,
            ResolvedTmpTypographyStyle style,
            TmpTypographyAuthoredState authoredState,
            TypographyApplyMask applyMask,
            TypographySizingSource sizingSource)
        {
            if (target == null)
            {
                return;
            }

            var appliesFont = (applyMask & TypographyApplyMask.Font) != TypographyApplyMask.None;
            var appliesFontStyle = (applyMask & TypographyApplyMask.FontStyle) != TypographyApplyMask.None;
            var fontAssetMaterialState = CaptureMaterialScaleRatios(
                appliesFont && style.FontAsset != null ? style.FontAsset.material : null);

            if (appliesFont && appliesFontStyle && target.fontStyle != FontStyles.Normal)
            {
                ApplyFontStylePreservingSharedMaterial(target, FontStyles.Normal, target.fontSharedMaterial);
            }

            if (appliesFont)
            {
                target.font = style.FontAsset != null ? style.FontAsset : authoredState.OriginalFont;
                fontAssetMaterialState.Restore();
            }

            if ((applyMask & TypographyApplyMask.Material) != TypographyApplyMask.None)
            {
                target.fontSharedMaterial = style.MaterialPreset != null
                    ? style.MaterialPreset
                    : authoredState.OriginalMaterial;
            }

            if (appliesFontStyle)
            {
                ApplyFontStylePreservingSharedMaterial(target, style.FontStyle, target.fontSharedMaterial);
                fontAssetMaterialState.Restore();
            }

            if ((applyMask & TypographyApplyMask.Sizing) != TypographyApplyMask.None)
            {
                ApplySizing(target, style, sizingSource);
            }

            if ((applyMask & TypographyApplyMask.LineSpacing) != TypographyApplyMask.None)
            {
                target.lineSpacing = style.LineSpacing;
            }

            if ((applyMask & TypographyApplyMask.CharacterSpacing) != TypographyApplyMask.None)
            {
                target.characterSpacing = style.CharacterSpacing;
            }
        }

        private static void ApplyFontStylePreservingSharedMaterial(
            TMP_Text target,
            FontStyles fontStyle,
            Material sharedMaterial)
        {
            var hasScaleRatioA = TryGetMaterialFloat(sharedMaterial, ScaleRatioAProperty, out var scaleRatioA);
            var hasScaleRatioB = TryGetMaterialFloat(sharedMaterial, ScaleRatioBProperty, out var scaleRatioB);
            var hasScaleRatioC = TryGetMaterialFloat(sharedMaterial, ScaleRatioCProperty, out var scaleRatioC);

            target.fontStyle = fontStyle;

            RestoreMaterialFloat(sharedMaterial, ScaleRatioAProperty, hasScaleRatioA, scaleRatioA);
            RestoreMaterialFloat(sharedMaterial, ScaleRatioBProperty, hasScaleRatioB, scaleRatioB);
            RestoreMaterialFloat(sharedMaterial, ScaleRatioCProperty, hasScaleRatioC, scaleRatioC);
        }

        private static bool TryGetMaterialFloat(Material material, int propertyId, out float value)
        {
            if (material != null && material.HasProperty(propertyId))
            {
                value = material.GetFloat(propertyId);
                return true;
            }

            value = 0f;
            return false;
        }

        private static void RestoreMaterialFloat(Material material, int propertyId, bool restore, float value)
        {
            if (restore && material != null)
            {
                material.SetFloat(propertyId, value);
            }
        }

        private static MaterialScaleRatios CaptureMaterialScaleRatios(Material material)
        {
            return new MaterialScaleRatios(
                material,
                TryGetMaterialFloat(material, ScaleRatioAProperty, out var scaleRatioA),
                scaleRatioA,
                TryGetMaterialFloat(material, ScaleRatioBProperty, out var scaleRatioB),
                scaleRatioB,
                TryGetMaterialFloat(material, ScaleRatioCProperty, out var scaleRatioC),
                scaleRatioC);
        }

        private readonly struct MaterialScaleRatios
        {
            private readonly Material material;
            private readonly bool hasScaleRatioA;
            private readonly float scaleRatioA;
            private readonly bool hasScaleRatioB;
            private readonly float scaleRatioB;
            private readonly bool hasScaleRatioC;
            private readonly float scaleRatioC;

            public MaterialScaleRatios(
                Material material,
                bool hasScaleRatioA,
                float scaleRatioA,
                bool hasScaleRatioB,
                float scaleRatioB,
                bool hasScaleRatioC,
                float scaleRatioC)
            {
                this.material = material;
                this.hasScaleRatioA = hasScaleRatioA;
                this.scaleRatioA = scaleRatioA;
                this.hasScaleRatioB = hasScaleRatioB;
                this.scaleRatioB = scaleRatioB;
                this.hasScaleRatioC = hasScaleRatioC;
                this.scaleRatioC = scaleRatioC;
            }

            public void Restore()
            {
                RestoreMaterialFloat(material, ScaleRatioAProperty, hasScaleRatioA, scaleRatioA);
                RestoreMaterialFloat(material, ScaleRatioBProperty, hasScaleRatioB, scaleRatioB);
                RestoreMaterialFloat(material, ScaleRatioCProperty, hasScaleRatioC, scaleRatioC);
            }
        }

        private static void ApplySizing(
            TMP_Text target,
            ResolvedTmpTypographyStyle style,
            TypographySizingSource sizingSource)
        {
            if (target == null ||
                sizingSource == TypographySizingSource.Authored ||
                style.SizingMode == TypographySizingMode.PreserveAuthored)
            {
                return;
            }

            switch (style.SizingMode)
            {
                case TypographySizingMode.Fixed:
                    target.fontSize = style.FixedSize;
                    break;

                case TypographySizingMode.AutoSizeRange:
                    target.enableAutoSizing = true;
                    target.fontSizeMin = style.MinSize;
                    target.fontSizeMax = style.MaxSize;
                    break;
            }
        }
    }
}
