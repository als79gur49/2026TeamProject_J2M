using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Tests
{
    internal static class SemanticEventAssertions
    {
        public static bool ContainsEvent(
            IReadOnlyList<string> entries,
            string eventKind,
            params string[] requiredFragments)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (IsMatch(entries[i], eventKind, requiredFragments))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ContainsEvent(
            string dump,
            string eventKind,
            params string[] requiredFragments)
        {
            if (string.IsNullOrEmpty(dump) || string.Equals(dump, "<empty>", StringComparison.Ordinal))
            {
                return false;
            }

            var lines = dump.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (IsMatch(lines[i].Trim(), eventKind, requiredFragments))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMatch(
            string entry,
            string eventKind,
            IReadOnlyList<string> requiredFragments)
        {
            if (string.IsNullOrEmpty(entry) ||
                !entry.StartsWith(eventKind, StringComparison.Ordinal) ||
                (entry.Length > eventKind.Length && entry[eventKind.Length] != '|'))
            {
                return false;
            }

            for (var i = 0; i < requiredFragments.Count; i++)
            {
                if (!entry.Contains(requiredFragments[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
