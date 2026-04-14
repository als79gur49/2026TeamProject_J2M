using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Feature.Gameplay.Loop;

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

        public static string[] FilterEvents(
            IReadOnlyList<string> entries,
            string eventKind)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var matches = new List<string>();
            for (var i = 0; i < entries.Count; i++)
            {
                if (IsMatch(entries[i], eventKind, Array.Empty<string>()))
                {
                    matches.Add(entries[i]);
                }
            }

            return matches.ToArray();
        }

        public static int[] GetCleanupRemovedEntityIds(IReadOnlyList<string> entries)
        {
            var cleanupEvents = FilterEvents(entries, "CleanupRemoved");
            var entityIds = new int[cleanupEvents.Length];
            for (var i = 0; i < cleanupEvents.Length; i++)
            {
                entityIds[i] = ParseIntField(cleanupEvents[i], "E=");
            }

            return entityIds;
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

        private static int ParseIntField(string entry, string prefix)
        {
            var startIndex = entry.IndexOf(prefix, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                throw new ArgumentException($"Entry does not contain required prefix '{prefix}'.", nameof(entry));
            }

            startIndex += prefix.Length;
            var endIndex = entry.IndexOf('|', startIndex);
            var value = endIndex >= 0
                ? entry.Substring(startIndex, endIndex - startIndex)
                : entry.Substring(startIndex);
            return int.Parse(value, CultureInfo.InvariantCulture);
        }
    }

    internal static class CleanupFixtureFactory
    {
        public static CleanupPhaseResult None()
        {
            return CleanupPhaseResult.Empty;
        }

        public static CleanupPhaseResult RemovedEntities(params int[] entityIds)
        {
            if (entityIds == null)
            {
                throw new ArgumentNullException(nameof(entityIds));
            }

            if (entityIds.Length == 0)
            {
                return CleanupPhaseResult.Empty;
            }

            return new CleanupPhaseResult(entityIds, Array.Empty<string>(), Array.Empty<string>());
        }
    }
}
