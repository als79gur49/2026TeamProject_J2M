using System;
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
        private readonly TMP_FontAsset _defaultFontAsset;
        private readonly Material _defaultMaterialPreset;
        private bool _isDisposed;

        public LocalizedTmpTextBinding(
            TMP_Text target,
            LocalizedTextDescriptor descriptor,
            ILocalizedTextResolver textResolver,
            ILocalizedTypographyResolver typographyResolver,
            ILocalizedTmpFontResolver fontResolver = null)
        {
            _target = target;
            _descriptor = descriptor;
            _textResolver = textResolver;
            _typographyResolver = typographyResolver ?? DefaultLocalizedTypographyResolver.Instance;
            _fontResolver = fontResolver;
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

    public static class LocalizedTmpTextApplicator
    {
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
    }
}
