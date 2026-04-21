using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Game.Feature.Stages
{
    public static class StageIdNormalizer
    {
        private static readonly Regex CanonicalPattern =
            new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

        public static string Normalize(string rawValue)
        {
            return TryNormalize(rawValue, out var canonicalValue, out _)
                ? canonicalValue
                : string.Empty;
        }

        public static bool TryNormalize(string rawValue, out string canonicalValue, out string error)
        {
            canonicalValue = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                error = "Stage id cannot be empty.";
                return false;
            }

            var trimmed = rawValue.Trim();
            var builder = new StringBuilder(trimmed.Length);
            var previousWasSeparator = false;

            for (var i = 0; i < trimmed.Length; i++)
            {
                var c = char.ToLowerInvariant(trimmed[i]);
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    previousWasSeparator = false;
                    continue;
                }

                if (char.IsWhiteSpace(c) || c == '_' || c == '/' || c == '\\' || c == '-')
                {
                    if (builder.Length == 0 || previousWasSeparator)
                    {
                        continue;
                    }

                    builder.Append('-');
                    previousWasSeparator = true;
                    continue;
                }

                builder.Append(c);
                previousWasSeparator = false;
            }

            canonicalValue = builder.ToString().Trim('-');
            if (!IsCanonical(canonicalValue))
            {
                error = $"'{rawValue}' normalizes to invalid canonical stage id '{canonicalValue}'.";
                canonicalValue = string.Empty;
                return false;
            }

            return true;
        }

        public static bool IsCanonical(string candidate)
        {
            return !string.IsNullOrEmpty(candidate) &&
                   CanonicalPattern.IsMatch(candidate);
        }
    }
}
