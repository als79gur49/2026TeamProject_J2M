using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Debug = UnityEngine.Debug;
using UnityProfiler = UnityEngine.Profiling.Profiler;

namespace Game.Feature.UI.Composition
{
    internal sealed class FontAllResidentPlayerProbe : MonoBehaviour
    {
        internal const string LaunchArgument = "--font-all-resident-probe";
        internal const string OutputArgument = "--font-all-resident-output";
        internal const string SuccessMarker = "FONT_ALL_RESIDENT_PROBE:PASS";
        internal const string FailureMarker = "FONT_ALL_RESIDENT_PROBE:FAIL";
        private const float ReadyTimeoutSeconds = 30f;
        private const int ExpectedDistinctFontCount = 11;
        private const int ExpectedTableEntryCount = 142;
        private static readonly string[] ExpectedRequiredLocales =
        {
            "en-US",
            "ko-KR",
            "ja-JP",
            "zh-CN",
        };
        private static readonly string[] LocaleSequence =
        {
            "en-US",
            "ko-KR",
            "ja-JP",
            "zh-CN",
            "en-US",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallWhenRequested()
        {
            if (!HasArgument(LaunchArgument))
            {
                return;
            }

            UnityEngine.Application.runInBackground = true;
            var root = new GameObject(nameof(FontAllResidentPlayerProbe));
            DontDestroyOnLoad(root);
            root.AddComponent<FontAllResidentPlayerProbe>();
        }

        private IEnumerator Start()
        {
            var report = new ProbeReport
            {
                utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = UnityEngine.Application.unityVersion,
                platform = UnityEngine.Application.platform.ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                expectedDistinctFontCount = ExpectedDistinctFontCount,
                localeSequence = LocaleSequence.ToArray(),
            };

            var outputDirectory = ReadArgumentValue(OutputArgument);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                Finish(report, string.Empty, "output directory argument is missing");
                yield break;
            }

            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, "font-all-resident-player-report.json");

            GameplayUiTypographyTheme theme = null;
            var deadline = Time.realtimeSinceStartup + ReadyTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                theme = Resources.FindObjectsOfTypeAll<GameplayUiTypographyTheme>()
                    .FirstOrDefault(candidate => candidate != null &&
                        string.Equals(candidate.name, "GameplayUiTypographyTheme", StringComparison.Ordinal));
                if (theme != null && LocalizationSettings.InitializationOperation.IsDone)
                {
                    break;
                }

                yield return null;
            }

            if (theme == null)
            {
                Finish(report, outputPath, "production GameplayUiTypographyTheme did not become resident");
                yield break;
            }

            if (!UnityStringTableTextResolver.TryCreateSettingsDefault(
                    new NoOpUiLocalePreferenceStore(),
                    out var resolver,
                    out var resolverFailure))
            {
                Finish(report, outputPath, $"production locale resolver failed: {resolverFailure}");
                yield break;
            }

            using (resolver)
            {
                var distinctFonts = CollectDistinctThemeFonts(theme);
                report.actualDistinctFontCount = distinctFonts.Length;
                report.requiredLocales = theme.RequiredLocaleCodes.ToArray();
                report.fonts = distinctFonts.Select(CaptureFont).ToList();
                report.before = CaptureMemory(distinctFonts);

                // TMP can cache fallback resolutions into a font's runtime lookup table while
                // generating text. Snapshot serialized direct coverage before any probe mesh is
                // generated so repeated locale transitions cannot change the admission result.
                var directCodePointsByFont = distinctFonts.ToDictionary(
                    font => font.GetInstanceID(),
                    font => new HashSet<uint>((font.characterTable ?? new List<TMP_Character>())
                        .Where(character => character != null)
                        .Select(character => character.unicode)));
                var missingGlyphMarker = TMP_Settings.missingGlyphCharacter == 0
                    ? 0x25a1u
                    : (uint)TMP_Settings.missingGlyphCharacter;
                report.missingGlyphMarkerCodePoint = $"U+{missingGlyphMarker:X}";
                var markerResolvableByFont = distinctFonts.ToDictionary(
                    font => font.GetInstanceID(),
                    font => CanResolveMissingGlyphMarker(font, missingGlyphMarker));

                var initialFontIds = distinctFonts.Select(font => font.GetInstanceID()).ToArray();
                var initialAtlasIds = distinctFonts
                    .SelectMany(font => font.atlasTextures ?? Array.Empty<Texture2D>())
                    .Where(texture => texture != null)
                    .Select(texture => texture.GetInstanceID())
                    .OrderBy(value => value)
                    .ToArray();

                var meshRoot = new GameObject("FontAllResidentProbeMesh");
                DontDestroyOnLoad(meshRoot);
                var meshText = meshRoot.AddComponent<TextMeshPro>();
                meshText.overflowMode = TextOverflowModes.Overflow;
                meshText.textWrappingMode = TextWrappingModes.NoWrap;
                meshText.GetComponent<Renderer>().enabled = false;

                foreach (var localeCode in LocaleSequence)
                {
                    var transition = RunTransition(
                        resolver,
                        theme,
                        initialFontIds,
                        initialAtlasIds,
                        directCodePointsByFont,
                        markerResolvableByFont,
                        meshText,
                        localeCode);
                    report.transitions.Add(transition);
                    yield return null;
                }

                Destroy(meshRoot);
                distinctFonts = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                yield return null;

                var residentFonts = CollectDistinctThemeFonts(theme);
                report.after = CaptureMemory(residentFonts);
                report.allResidentAfterSequence = HasSameResidentGraph(
                    residentFonts,
                    initialFontIds,
                    initialAtlasIds);
                report.pass = report.actualDistinctFontCount == ExpectedDistinctFontCount &&
                              report.requiredLocales.SequenceEqual(ExpectedRequiredLocales) &&
                              report.fonts.All(font =>
                                  string.Equals(font.atlasPopulationMode, AtlasPopulationMode.Static.ToString(), StringComparison.Ordinal) &&
                                  font.atlasCount == 1 &&
                                  font.fallbackCount == 0 &&
                                  font.atlases.Count == 1 &&
                                  !font.atlases[0].readable) &&
                              report.before.atlasCount == ExpectedDistinctFontCount &&
                              report.before.readableAtlasCount == 0 &&
                              report.allResidentAfterSequence &&
                              report.transitions.Count == LocaleSequence.Length &&
                              report.transitions.All(transition =>
                                  transition.selectionAccepted &&
                                  transition.selectedLocaleMatched &&
                                  transition.tableEntryCount == ExpectedTableEntryCount &&
                                  transition.nullTableCount == 0 &&
                                  transition.glyphAdmissionPassed &&
                                  transition.allFontsStillResident) &&
                              report.after.atlasCount == ExpectedDistinctFontCount &&
                              report.after.readableAtlasCount == 0;
                report.failure = report.pass
                    ? string.Empty
                    : "one or more font residency, locale transition, or glyph admission checks failed";
            }

            WriteReport(outputPath, report);
            Debug.Log(report.pass ? SuccessMarker : $"{FailureMarker} {report.failure}");
            UnityEngine.Application.Quit(report.pass ? 0 : 1);
        }

        private static TransitionRecord RunTransition(
            UnityStringTableTextResolver resolver,
            GameplayUiTypographyTheme theme,
            IReadOnlyList<int> initialFontIds,
            IReadOnlyList<int> initialAtlasIds,
            IReadOnlyDictionary<int, HashSet<uint>> directCodePointsByFont,
            IReadOnlyDictionary<int, bool> markerResolvableByFont,
            TextMeshPro meshText,
            string localeCode)
        {
            var record = new TransitionRecord { locale = localeCode };
            var total = Stopwatch.StartNew();
            var selection = Stopwatch.StartNew();
            record.selectionAccepted = resolver.TrySetLocale(localeCode);
            selection.Stop();
            record.selectionMilliseconds = selection.Elapsed.TotalMilliseconds;
            record.selectedLocale = LocalizationSettings.SelectedLocale?.Identifier.Code ?? string.Empty;
            record.selectedLocaleMatched = string.Equals(
                record.selectedLocale,
                localeCode,
                StringComparison.Ordinal);

            var corpusTimer = Stopwatch.StartNew();
            var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
            var tables = new[]
            {
                LocalizationSettings.StringDatabase.GetTable("UI", locale),
                LocalizationSettings.StringDatabase.GetTable("Stage", locale),
            };
            record.tableEntryCount = tables.Where(table => table != null).Sum(table => table.Values.Count);
            record.nullTableCount = tables.Count(table => table == null);
            var productionCorpus = string.Concat(
                tables.Where(table => table != null).Select(table => table.GenerateCharacterSet()));
            var productionCodePoints = EnumerateCodePoints(productionCorpus)
                .Where(codePoint => !IsIgnoredControl(codePoint))
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            record.productionCorpusCodePointCount = productionCodePoints.Length;

            var localeFonts = theme.LocaleFontSets
                .Where(set => set != null && string.Equals(set.LocaleCode, localeCode, StringComparison.Ordinal))
                .SelectMany(set => set.Entries ?? new List<LocaleFontEntry>())
                .Where(entry => entry != null && entry.FontAsset != null)
                .Select(entry => entry.FontAsset)
                .Distinct()
                .OrderBy(font => font.name, StringComparer.Ordinal)
                .ToArray();
            record.localeFontCount = localeFonts.Length;
            foreach (var font in localeFonts)
            {
                var fontId = font.GetInstanceID();
                var directCodePoints = directCodePointsByFont[fontId];
                var missing = productionCodePoints
                    .Where(codePoint => !directCodePoints.Contains((uint)codePoint))
                    .ToArray();
                if (missing.Length > 0)
                {
                    record.productionMissingGlyphs.Add(new MissingGlyphRecord
                    {
                        font = font.name,
                        codePoints = missing.Select(value => $"U+{value:X}").ToArray(),
                    });
                }

                if (!directCodePoints.Contains(0x25a1u))
                {
                    record.sentinelDirectMissingFonts.Add(font.name);
                }

                if (!markerResolvableByFont[fontId])
                {
                    record.sentinelMissingFonts.Add(font.name);
                }
            }

            record.productionPerFontMissingGlyphCount = record.productionMissingGlyphs
                .Sum(item => item.codePoints.Length);
            record.productionUnionMissingCodePoints = productionCodePoints
                .Where(codePoint => localeFonts.All(font =>
                    !directCodePointsByFont[font.GetInstanceID()].Contains((uint)codePoint)))
                .Select(value => $"U+{value:X}")
                .ToArray();
            record.productionUnionMissingGlyphCount = record.productionUnionMissingCodePoints.Length;
            record.sentinelDirectMissingFontCount = record.sentinelDirectMissingFonts.Count;
            record.sentinelMissingFontCount = record.sentinelMissingFonts.Count;
            record.glyphAdmissionPassed =
                record.productionUnionMissingGlyphCount == 0 &&
                record.productionPerFontMissingGlyphCount == 0 &&
                record.sentinelMissingFontCount == 0;
            corpusTimer.Stop();
            record.tableAndGlyphMilliseconds = corpusTimer.Elapsed.TotalMilliseconds;

            var meshTimer = Stopwatch.StartNew();
            foreach (var font in localeFonts)
            {
                meshText.font = font;
                meshText.text = productionCorpus + "\u25a1";
                meshText.ForceMeshUpdate(true, true);
                record.generatedMeshCharacterCount += meshText.textInfo.characterCount;
            }

            meshTimer.Stop();
            record.meshGenerationMilliseconds = meshTimer.Elapsed.TotalMilliseconds;
            total.Stop();
            record.totalSynchronousMilliseconds = total.Elapsed.TotalMilliseconds;
            var residentFonts = CollectDistinctThemeFonts(theme);
            record.allFontsStillResident = HasSameResidentGraph(
                residentFonts,
                initialFontIds,
                initialAtlasIds);
            record.memory = CaptureMemory(residentFonts);
            return record;
        }

        private static TMP_FontAsset[] CollectDistinctThemeFonts(
            GameplayUiTypographyTheme theme)
        {
            return theme.LocaleFontSets
                .Where(set => set != null)
                .SelectMany(set => set.Entries ?? new List<LocaleFontEntry>())
                .Where(entry => entry != null && entry.FontAsset != null)
                .Select(entry => entry.FontAsset)
                .Distinct()
                .OrderBy(font => font.name, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool CanResolveMissingGlyphMarker(TMP_FontAsset sourceFont, uint marker)
        {
            if (sourceFont == null)
            {
                return false;
            }

            if (TMP_FontAssetUtilities.GetCharacterFromFontAsset(
                    marker,
                    sourceFont,
                    true,
                    FontStyles.Normal,
                    FontWeight.Regular,
                    out _) != null)
            {
                return true;
            }

            var globalFallbacks = TMP_Settings.fallbackFontAssets;
            if (globalFallbacks != null)
            {
                foreach (var fallback in globalFallbacks)
                {
                    if (fallback != null && TMP_FontAssetUtilities.GetCharacterFromFontAsset(
                            marker,
                            fallback,
                            true,
                            FontStyles.Normal,
                            FontWeight.Regular,
                            out _) != null)
                    {
                        return true;
                    }
                }
            }

            var defaultFont = TMP_Settings.defaultFontAsset;
            return defaultFont != null && TMP_FontAssetUtilities.GetCharacterFromFontAsset(
                marker,
                defaultFont,
                true,
                FontStyles.Normal,
                FontWeight.Regular,
                out _) != null;
        }

        private static FontRecord CaptureFont(TMP_FontAsset font)
        {
            var atlases = font.atlasTextures ?? Array.Empty<Texture2D>();
            return new FontRecord
            {
                name = font.name,
                instanceId = font.GetInstanceID(),
                atlasPopulationMode = font.atlasPopulationMode.ToString(),
                atlasCount = atlases.Count(texture => texture != null),
                fallbackCount = font.fallbackFontAssetTable?.Count ?? 0,
                nativeBytes = UnityProfiler.GetRuntimeMemorySizeLong(font),
                atlases = atlases.Where(texture => texture != null).Select(texture => new AtlasRecord
                {
                    name = texture.name,
                    instanceId = texture.GetInstanceID(),
                    width = texture.width,
                    height = texture.height,
                    mipCount = texture.mipmapCount,
                    readable = texture.isReadable,
                    format = texture.format.ToString(),
                    graphicsFormat = texture.graphicsFormat.ToString(),
                    nativeBytes = UnityProfiler.GetRuntimeMemorySizeLong(texture),
                }).ToList(),
            };
        }

        private static MemoryRecord CaptureMemory(IEnumerable<TMP_FontAsset> fonts)
        {
            var fontArray = fonts.Where(font => font != null).Distinct().ToArray();
            var atlases = fontArray
                .SelectMany(font => font.atlasTextures ?? Array.Empty<Texture2D>())
                .Where(texture => texture != null)
                .Distinct()
                .ToArray();
            return new MemoryRecord
            {
                managedBytes = GC.GetTotalMemory(false),
                processWorkingSetBytes = Process.GetCurrentProcess().WorkingSet64,
                unityAllocatedBytes = UnityProfiler.GetTotalAllocatedMemoryLong(),
                unityReservedBytes = UnityProfiler.GetTotalReservedMemoryLong(),
                graphicsDriverAllocatedBytes = UnityProfiler.GetAllocatedMemoryForGraphicsDriver(),
                currentTextureMemoryBytes = (long)Texture.currentTextureMemory,
                totalTextureMemoryBytes = (long)Texture.totalTextureMemory,
                fontNativeBytes = fontArray.Sum(UnityProfiler.GetRuntimeMemorySizeLong),
                atlasNativeBytes = atlases.Sum(UnityProfiler.GetRuntimeMemorySizeLong),
                atlasCount = atlases.Length,
                readableAtlasCount = atlases.Count(texture => texture.isReadable),
            };
        }

        private static bool HasSameResidentGraph(
            IReadOnlyList<TMP_FontAsset> fonts,
            IReadOnlyList<int> initialFontIds,
            IReadOnlyList<int> initialAtlasIds)
        {
            if (fonts.Count != initialFontIds.Count)
            {
                return false;
            }

            for (var index = 0; index < fonts.Count; index++)
            {
                if (fonts[index] == null || fonts[index].GetInstanceID() != initialFontIds[index])
                {
                    return false;
                }
            }

            var atlasIds = fonts
                .SelectMany(font => font.atlasTextures ?? Array.Empty<Texture2D>())
                .Where(texture => texture != null)
                .Select(texture => texture.GetInstanceID())
                .OrderBy(value => value)
                .ToArray();
            return atlasIds.SequenceEqual(initialAtlasIds);
        }

        private static IEnumerable<int> EnumerateCodePoints(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            for (var index = 0; index < text.Length; index++)
            {
                var first = text[index];
                if (char.IsHighSurrogate(first) &&
                    index + 1 < text.Length &&
                    char.IsLowSurrogate(text[index + 1]))
                {
                    yield return char.ConvertToUtf32(first, text[++index]);
                }
                else if (!char.IsSurrogate(first))
                {
                    yield return first;
                }
            }
        }

        private static bool IsIgnoredControl(int codePoint)
        {
            return codePoint == '\r' || codePoint == '\n' || codePoint == '\t';
        }

        private static void Finish(ProbeReport report, string outputPath, string failure)
        {
            report.pass = false;
            report.failure = failure;
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                WriteReport(outputPath, report);
            }

            Debug.LogError($"{FailureMarker} {failure}");
            UnityEngine.Application.Quit(1);
        }

        private static void WriteReport(string outputPath, ProbeReport report)
        {
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
        }

        private static bool HasArgument(string expected)
        {
            return Environment.GetCommandLineArgs().Any(argument =>
                string.Equals(argument, expected, StringComparison.Ordinal));
        }

        private static string ReadArgumentValue(string argumentName)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], argumentName, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return string.Empty;
        }

        [Serializable]
        private sealed class ProbeReport
        {
            public bool pass;
            public string failure = string.Empty;
            public string utc = string.Empty;
            public string unityVersion = string.Empty;
            public string platform = string.Empty;
            public string graphicsDevice = string.Empty;
            public string graphicsDeviceName = string.Empty;
            public int expectedDistinctFontCount;
            public int actualDistinctFontCount;
            public string[] requiredLocales = Array.Empty<string>();
            public string[] localeSequence = Array.Empty<string>();
            public string missingGlyphMarkerCodePoint = string.Empty;
            public bool allResidentAfterSequence;
            public List<FontRecord> fonts = new();
            public List<TransitionRecord> transitions = new();
            public MemoryRecord before;
            public MemoryRecord after;
        }

        [Serializable]
        private sealed class FontRecord
        {
            public string name = string.Empty;
            public int instanceId;
            public string atlasPopulationMode = string.Empty;
            public int atlasCount;
            public int fallbackCount;
            public long nativeBytes;
            public List<AtlasRecord> atlases = new();
        }

        [Serializable]
        private sealed class AtlasRecord
        {
            public string name = string.Empty;
            public int instanceId;
            public int width;
            public int height;
            public int mipCount;
            public bool readable;
            public string format = string.Empty;
            public string graphicsFormat = string.Empty;
            public long nativeBytes;
        }

        [Serializable]
        private sealed class TransitionRecord
        {
            public string locale = string.Empty;
            public bool selectionAccepted;
            public string selectedLocale = string.Empty;
            public bool selectedLocaleMatched;
            public int tableEntryCount;
            public int nullTableCount;
            public int productionCorpusCodePointCount;
            public int localeFontCount;
            public bool glyphAdmissionPassed;
            public int productionPerFontMissingGlyphCount;
            public int productionUnionMissingGlyphCount;
            public string[] productionUnionMissingCodePoints = Array.Empty<string>();
            public int sentinelDirectMissingFontCount;
            public List<string> sentinelDirectMissingFonts = new();
            public int sentinelMissingFontCount;
            public List<string> sentinelMissingFonts = new();
            public int generatedMeshCharacterCount;
            public bool allFontsStillResident;
            public double selectionMilliseconds;
            public double tableAndGlyphMilliseconds;
            public double meshGenerationMilliseconds;
            public double totalSynchronousMilliseconds;
            public List<MissingGlyphRecord> productionMissingGlyphs = new();
            public MemoryRecord memory;
        }

        [Serializable]
        private sealed class MissingGlyphRecord
        {
            public string font = string.Empty;
            public string[] codePoints = Array.Empty<string>();
        }

        [Serializable]
        private sealed class MemoryRecord
        {
            public long managedBytes;
            public long processWorkingSetBytes;
            public long unityAllocatedBytes;
            public long unityReservedBytes;
            public long graphicsDriverAllocatedBytes;
            public long currentTextureMemoryBytes;
            public long totalTextureMemoryBytes;
            public long fontNativeBytes;
            public long atlasNativeBytes;
            public int atlasCount;
            public int readableAtlasCount;
        }
    }
}
