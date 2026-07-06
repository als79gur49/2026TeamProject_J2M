using System;
using Game.Feature.UI.ViewShared;
using TMPro;

namespace Game.Feature.UI.Screens
{
    public sealed class LocalizedTmpTextBinding : IDisposable
    {
        private readonly TMP_Text _target;
        private readonly LocalizedTextDescriptor _descriptor;
        private readonly ILocalizedTextResolver _textResolver;
        private readonly ILocalizedTypographyResolver _typographyResolver;
        private bool _isDisposed;

        public LocalizedTmpTextBinding(
            TMP_Text target,
            LocalizedTextDescriptor descriptor,
            ILocalizedTextResolver textResolver,
            ILocalizedTypographyResolver typographyResolver)
        {
            _target = target;
            _descriptor = descriptor;
            _textResolver = textResolver;
            _typographyResolver = typographyResolver ?? DefaultLocalizedTypographyResolver.Instance;

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

            LocalizedTmpTextApplicator.ApplyTypography(
                _target,
                _typographyResolver.Resolve(
                    _textResolver != null ? _textResolver.CurrentLocaleCode : string.Empty,
                    _descriptor.Role,
                    _descriptor.Weight));
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
    }
}
