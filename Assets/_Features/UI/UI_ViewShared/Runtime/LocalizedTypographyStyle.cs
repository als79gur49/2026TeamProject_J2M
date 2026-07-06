using System;

namespace Game.Feature.UI.ViewShared
{
    public readonly struct LocalizedTypographyStyle : IEquatable<LocalizedTypographyStyle>
    {
        public LocalizedTypographyStyle(float fontSize, float lineSpacing, bool bold)
        {
            FontSize = fontSize;
            LineSpacing = lineSpacing;
            Bold = bold;
        }

        public float FontSize { get; }

        public float LineSpacing { get; }

        public bool Bold { get; }

        public bool Equals(LocalizedTypographyStyle other)
        {
            return FontSize.Equals(other.FontSize) &&
                   LineSpacing.Equals(other.LineSpacing) &&
                   Bold == other.Bold;
        }

        public override bool Equals(object obj)
        {
            return obj is LocalizedTypographyStyle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(FontSize, LineSpacing, Bold);
        }
    }

    public interface ILocalizedTypographyResolver
    {
        LocalizedTypographyStyle Resolve(
            string localeCode,
            LocalizedTextRole role,
            LocalizedTextWeight weight);
    }

    public sealed class DefaultLocalizedTypographyResolver : ILocalizedTypographyResolver
    {
        public static readonly DefaultLocalizedTypographyResolver Instance = new();

        private DefaultLocalizedTypographyResolver()
        {
        }

        public LocalizedTypographyStyle Resolve(
            string localeCode,
            LocalizedTextRole role,
            LocalizedTextWeight weight)
        {
            var bold = weight == LocalizedTextWeight.Bold;
            switch (role)
            {
                case LocalizedTextRole.Title:
                    return new LocalizedTypographyStyle(36f, 0f, bold);

                case LocalizedTextRole.Subtitle:
                    return new LocalizedTypographyStyle(26f, 0f, bold);

                case LocalizedTextRole.Button:
                    return new LocalizedTypographyStyle(20f, 0f, bold);

                case LocalizedTextRole.Label:
                case LocalizedTextRole.Body:
                default:
                    return new LocalizedTypographyStyle(18f, 0f, bold);
            }
        }
    }
}
