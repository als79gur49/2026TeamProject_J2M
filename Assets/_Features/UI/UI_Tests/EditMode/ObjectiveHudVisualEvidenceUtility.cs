using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Composition.Editor;
using Game.Feature.UI.HUD;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Feature.UI.Tests
{
    public static class ObjectiveHudVisualEvidenceUtility
    {
        public const string CommandLineEntryPoint =
            "Game.Feature.UI.Tests.ObjectiveHudVisualEvidenceUtility.CaptureFromCommandLine";
        public const string HudPrefabPath =
            "Assets/_Features/UI/UI_HUD/Prefabs/GameplayHudRoot.prefab";
        public const string ClimateFontPath =
            "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000 SDF.asset";
        public const string ManifestFileName = "objective-hud-capture.log";

        private static readonly CaptureScenario[] Scenarios =
        {
            new("Idle", "en-US", expectedRowCount: 2),
            new("Idle", "ko-KR", expectedRowCount: 2),
            new("MaxStack", "en-US", expectedRowCount: 3),
            new("MaxStack", "ko-KR", expectedRowCount: 3),
        };

        public static void CaptureFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var outputDirectory = ReadArgument(args, "-objectiveHudVisualOutput");
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("-objectiveHudVisualOutput is required.");
            }

            var width = ReadPositiveInt(args, "-objectiveHudVisualWidth", 1920);
            var height = ReadPositiveInt(args, "-objectiveHudVisualHeight", 1080);
            Capture(outputDirectory, width, height);
        }

        public static void Capture(string outputDirectory, int width, int height)
        {
            outputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(outputDirectory);
            var gitHead = TypographyPreviewScreenshotManifestUtility.ReadCurrentGitHead();
            var errors = new List<string>();
            var captures = new List<CaptureRecord>();

            using (var assetGuard = AssetFileRestoreGuard.Capture(ClimateFontPath))
            {
                foreach (var scenario in Scenarios)
                {
                    try
                    {
                        captures.Add(CaptureScenarioImage(
                            scenario,
                            outputDirectory,
                            width,
                            height));
                    }
                    catch (Exception exception)
                    {
                        errors.Add($"{scenario.State} {scenario.Locale}: {exception}");
                    }
                }

                foreach (var localeCaptures in captures.GroupBy(capture => capture.Scenario.Locale))
                {
                    var stateHashes = localeCaptures
                        .Select(capture => capture.Sha256)
                        .Distinct(StringComparer.Ordinal)
                        .Count();
                    if (localeCaptures.Count() > 1 && stateHashes != localeCaptures.Count())
                    {
                        errors.Add(
                            $"{localeCaptures.Key}: visual states produced duplicate PNG hashes.");
                    }
                }
            }

            var manifestPath = WriteManifest(
                outputDirectory,
                width,
                height,
                gitHead,
                captures,
                errors);
            Debug.Log($"Objective HUD visual manifest: {manifestPath}");
            if (errors.Count > 0 ||
                captures.Count != Scenarios.Length ||
                captures.Any(capture => !capture.Passed))
            {
                throw new InvalidOperationException(
                    $"Objective HUD visual evidence failed. See {manifestPath}");
            }
        }

        private static CaptureRecord CaptureScenarioImage(
            CaptureScenario scenario,
            string outputDirectory,
            int width,
            int height)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Gameplay HUD prefab was not found at {HudPrefabPath}.");
            }

            var climate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ClimateFontPath);
            if (climate == null)
            {
                throw new InvalidOperationException($"Climate font was not found at {ClimateFontPath}.");
            }

            var prefabHud = prefab.GetComponent<HUDRootView>();
            var prefabObjective = prefabHud.ObjectiveHudView;
            var prefabHeader = prefabObjective.HeaderLabel;
            var prefabRowLabel = GetTemplateRow(prefabObjective).GetComponentInChildren<TMP_Text>(true);
            var englishHeaderFont = prefabHeader.font;
            var englishHeaderMaterial = prefabHeader.fontSharedMaterial;
            var englishHeaderStyle = prefabHeader.fontStyle;
            var englishRowFont = prefabRowLabel.font;
            var englishRowMaterial = prefabRowLabel.fontSharedMaterial;
            var englishRowStyle = prefabRowLabel.fontStyle;

            GameObject root = null;
            UnityStringTableTextResolver resolver = null;
            HUDRootPresenter rootPresenter = null;
            Texture2D texture = null;
            try
            {
                if (!UnityStringTableTextResolver.TryCreateSettingsDefault(
                        new MemoryLocalePreferenceStore(scenario.Locale),
                        out resolver,
                        out var failureReason))
                {
                    throw new InvalidOperationException(
                        $"Production Unity String Table resolver could not initialize: {failureReason}");
                }

                if (!resolver.TrySetLocale(scenario.Locale))
                {
                    throw new InvalidOperationException(
                        $"Production resolver rejected locale '{scenario.Locale}'.");
                }

                root = Object.Instantiate(prefab);
                root.name = $"ObjectiveHudVisual_{scenario.State}_{scenario.Locale}";
                var hud = root.GetComponent<HUDRootView>();
                hud.ValidateAuthoredStructureOrThrow();
                var objectiveView = hud.ObjectiveHudView;
                var typography = objectiveView.GetComponent<ObjectiveHudTypographyBinding>();
                typography.ValidateAuthoredStructureOrThrow();
                typography.Initialize(resolver);
                objectiveView.ConfigureTypography(typography);

                var initialReadModel = CreateReadModel(scenario, completePrimary: false);
                var initialSnapshot = CreateSnapshot(initialReadModel);
                var source = new EvidencePresentationSource(initialSnapshot);
                var stagePresenter = new StageInfoPresenter(resolver);
                var objectivePresenter = new ObjectiveHudPresenter(resolver);
                rootPresenter = new HUDRootPresenter(
                    source,
                    stagePresenter,
                    objectivePresenter,
                    new ChancePanelPresenter(),
                    new SurfaceBeltIndicatorPresenter(),
                    new PlayerStatusPresenter());

                hud.Bind(rootPresenter.ViewModel);
                hud.BindStageInfo(stagePresenter.ViewModel);
                objectiveView.Bind(objectivePresenter.ViewModel);
                SettleObjectiveRows(objectiveView);

                ForceLayoutAndText(root);
                var activeRows = GetActiveRows(objectiveView);
                ValidatePresentation(
                    scenario,
                    objectiveView,
                    activeRows,
                    climate,
                    englishHeaderFont,
                    englishHeaderMaterial,
                    englishHeaderStyle,
                    englishRowFont,
                    englishRowMaterial,
                    englishRowStyle);

                var options = new TypographyPreviewScreenshotOptions
                {
                    Width = width,
                    Height = height,
                    BackgroundColor = new Color(0.015f, 0.025f, 0.045f, 1f),
                };
                texture = TypographyPreviewScreenshotUtility.CaptureRootForValidation(root, options);
                var pngBytes = texture.EncodeToPNG();
                var fileName = $"HUD_Objectives_{scenario.State}_{scenario.Locale}.png";
                var filePath = Path.Combine(outputDirectory, fileName);
                File.WriteAllBytes(filePath, pngBytes);

                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!decoded.LoadImage(pngBytes) || decoded.width != width || decoded.height != height)
                    {
                        throw new InvalidOperationException(
                            $"{fileName} did not decode as {width}x{height}.");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(decoded);
                }

                var nonBlank = SystemInfo.graphicsDeviceType ==
                               UnityEngine.Rendering.GraphicsDeviceType.Null ||
                               HasNonBlankPixels(texture);
                if (!nonBlank)
                {
                    throw new InvalidOperationException($"{fileName} is blank or single-color.");
                }

                var headerIdentity = ReadIdentity(objectiveView.HeaderLabel);
                var rowIdentity = ReadIdentity(activeRows[0].Label);
                return new CaptureRecord(
                    scenario,
                    fileName,
                    pngBytes.LongLength,
                    ComputeSha256(pngBytes),
                    headerIdentity,
                    rowIdentity,
                    nonBlank ? "PASS" : "FAIL",
                    "PASS",
                    "PASS",
                    string.Empty);
            }
            finally
            {
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }

                rootPresenter?.Dispose();
                resolver?.Dispose();
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static GameplayObjectiveReadModel CreateReadModel(
            CaptureScenario scenario,
            bool completePrimary)
        {
            var conditions = new List<GameplayObjectiveConditionReadModel>
            {
                CreateCondition(
                    "reach-exit",
                    GameplayObjectivePresentationKind.ReachExit,
                    "reach-exit",
                    GameplayObjectiveConditionRole.PrimaryGoal,
                    completePrimary,
                    completePrimary ? 1 : 0,
                    1,
                    0),
                CreateCondition(
                    "button-group",
                    GameplayObjectivePresentationKind.ActivateButton,
                    "activate-button|role-2",
                    GameplayObjectiveConditionRole.SecondaryGoal,
                    false,
                    string.Equals(scenario.State, "MaxStack", StringComparison.Ordinal) ? 9 : 0,
                    string.Equals(scenario.State, "MaxStack", StringComparison.Ordinal) ? 10 : 1,
                    10),
            };

            if (string.Equals(scenario.State, "MaxStack", StringComparison.Ordinal))
            {
                conditions.Add(CreateCondition(
                    "moon-button-group",
                    GameplayObjectivePresentationKind.ActivateMoonButton,
                    "activate-moon-button|role-2",
                    GameplayObjectiveConditionRole.SecondaryGoal,
                    false,
                    98,
                    99,
                    20));
            }

            return new GameplayObjectiveReadModel(
                hasObjective: true,
                goalReached: completePrimary,
                allConditionsSatisfied: false,
                isCleared: false,
                conditions: conditions);
        }

        private static GameplayObjectiveConditionReadModel CreateCondition(
            string stableId,
            GameplayObjectivePresentationKind kind,
            string groupKey,
            GameplayObjectiveConditionRole role,
            bool isSatisfied,
            int completedCount,
            int requiredCount,
            int sortOrder)
        {
            return new GameplayObjectiveConditionReadModel(
                stableId,
                kind,
                groupKey,
                role,
                required: true,
                isSatisfied,
                completedCount,
                requiredCount,
                sortOrder);
        }

        private static UIPresentationSnapshot CreateSnapshot(GameplayObjectiveReadModel readModel)
        {
            var empty = UIPresentationSnapshot.Empty;
            var objective = UIStateMapper.MapObjectiveForPresentation(
                readModel,
                new StageId("objective-hud-visual"));
            return new UIPresentationSnapshot(
                empty.Tick,
                empty.Interaction,
                empty.Stage,
                objective,
                empty.Chance,
                empty.Topology,
                empty.SurfaceBelt,
                empty.Player,
                empty.Notifications);
        }

        private static void SettleObjectiveRows(ObjectiveHudView view)
        {
            var processAdvance = typeof(ObjectiveHudView).GetMethod(
                "ProcessTransitionAdvance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (processAdvance == null)
            {
                throw new MissingMethodException(nameof(ObjectiveHudView), "ProcessTransitionAdvance");
            }

            for (var iteration = 0; iteration < 64; iteration++)
            {
                var rows = GetActiveRows(view);
                foreach (var row in rows)
                {
                    var animator = row.View.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.Update(5f);
                    }

                    row.View.Tick(5f);
                }

                processAdvance.Invoke(view, new object[] { float.MaxValue });
            }
        }

        private static void ForceLayoutAndText(GameObject root)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            }

            Canvas.ForceUpdateCanvases();
        }

        private static void ValidatePresentation(
            CaptureScenario scenario,
            ObjectiveHudView view,
            IReadOnlyList<ActiveRow> activeRows,
            TMP_FontAsset climate,
            TMP_FontAsset englishHeaderFont,
            Material englishHeaderMaterial,
            FontStyles englishHeaderStyle,
            TMP_FontAsset englishRowFont,
            Material englishRowMaterial,
            FontStyles englishRowStyle)
        {
            var expectedHeader = string.Equals(scenario.Locale, "ko-KR", StringComparison.Ordinal)
                ? "과업"
                : "Objectives";
            if (!string.Equals(view.HeaderLabel.text, expectedHeader, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Header mismatch. Expected '{expectedHeader}', actual '{view.HeaderLabel.text}'.");
            }

            if (activeRows.Count != scenario.ExpectedRowCount)
            {
                throw new InvalidOperationException(
                    $"Expected {scenario.ExpectedRowCount} active rows, found {activeRows.Count}.");
            }

            if (string.Equals(scenario.Locale, "ko-KR", StringComparison.Ordinal))
            {
                AssertIdentity(view.HeaderLabel, climate, climate.material, FontStyles.Normal, "ko-KR header");
                foreach (var row in activeRows)
                {
                    AssertIdentity(row.Label, climate, climate.material, FontStyles.Normal, "ko-KR row");
                }
            }
            else
            {
                AssertIdentity(
                    view.HeaderLabel,
                    englishHeaderFont,
                    englishHeaderMaterial,
                    englishHeaderStyle,
                    "en-US header");
                foreach (var row in activeRows)
                {
                    AssertIdentity(
                        row.Label,
                        englishRowFont,
                        englishRowMaterial,
                        englishRowStyle,
                        "en-US row");
                }
            }

            var objectiveLayout = view.GetComponent<LayoutElement>();
            var rowHeight = GetTemplateRow(view).GetComponent<LayoutElement>().preferredHeight;
            var requiredHeight = view.HeaderLabel.rectTransform.rect.height + activeRows.Count * rowHeight;
            if (objectiveLayout == null || objectiveLayout.preferredHeight + 0.01f < requiredHeight)
            {
                throw new InvalidOperationException(
                    $"Objective viewport height {objectiveLayout?.preferredHeight ?? 0f} is below required {requiredHeight}.");
            }

            ValidateViewportGeometry(view, activeRows);

            foreach (var text in new[] { view.HeaderLabel }.Concat(activeRows.Select(row => row.Label)))
            {
                if (!text.isActiveAndEnabled ||
                    text.color.a <= 0f ||
                    text.canvasRenderer.cull)
                {
                    throw new InvalidOperationException(
                        $"{text.name} is not visible in the production HUD composition.");
                }

                var missing = text.text
                    .Where(character => !char.IsControl(character) && !char.IsWhiteSpace(character))
                    .Where(character => !text.font.HasCharacter(
                        character,
                        searchFallbacks: false,
                        tryAddCharacter: false))
                    .Distinct()
                    .ToArray();
                if (missing.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"{text.name} is missing glyphs: {string.Join(", ", missing.Select(value => $"U+{(int)value:X4}"))}.");
                }

                var availableWidth = objectiveLayout.preferredWidth + text.rectTransform.sizeDelta.x;
                var preferred = text.GetPreferredValues(text.text, Mathf.Max(1f, availableWidth), 0f);
                var availableHeight = ReferenceEquals(text, view.HeaderLabel)
                    ? text.rectTransform.rect.height
                    : rowHeight;
                if (preferred.y > availableHeight + 0.01f)
                {
                    throw new InvalidOperationException(
                        $"{text.name} preferred height {preferred.y} exceeds {availableHeight}.");
                }
            }
        }

        private static void ValidateViewportGeometry(
            ObjectiveHudView view,
            IReadOnlyList<ActiveRow> activeRows)
        {
            if (!(view.transform is RectTransform viewport))
            {
                throw new InvalidOperationException("Objective HUD root is not a RectTransform.");
            }

            var presentationRects = new List<PresentationRect>
            {
                new PresentationRect("header", view.HeaderLabel.rectTransform),
            };
            presentationRects.AddRange(activeRows.Select(row =>
                new PresentationRect(
                    $"row '{row.View.StableId}'",
                    row.View.transform as RectTransform)));

            var viewportRect = viewport.rect;
            var projected = new List<ProjectedPresentationRect>(presentationRects.Count);
            foreach (var presentationRect in presentationRects)
            {
                if (presentationRect.Target == null)
                {
                    throw new InvalidOperationException(
                        $"{presentationRect.Name} has no RectTransform.");
                }

                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    viewport,
                    presentationRect.Target);
                var rect = Rect.MinMaxRect(
                    bounds.min.x,
                    bounds.min.y,
                    bounds.max.x,
                    bounds.max.y);
                if (rect.width <= 0f || rect.height <= 0f)
                {
                    throw new InvalidOperationException(
                        $"{presentationRect.Name} has invalid bounds {rect}.");
                }

                const float tolerance = 0.01f;
                if (rect.xMin < viewportRect.xMin - tolerance ||
                    rect.xMax > viewportRect.xMax + tolerance ||
                    rect.yMin < viewportRect.yMin - tolerance ||
                    rect.yMax > viewportRect.yMax + tolerance)
                {
                    throw new InvalidOperationException(
                        $"{presentationRect.Name} bounds {rect} escape Objective viewport {viewportRect}.");
                }

                projected.Add(new ProjectedPresentationRect(presentationRect.Name, rect));
            }

            projected.Sort((left, right) => right.Rect.yMax.CompareTo(left.Rect.yMax));
            for (var index = 1; index < projected.Count; index++)
            {
                var previous = projected[index - 1];
                var current = projected[index];
                if (current.Rect.yMax > previous.Rect.yMin + 0.01f)
                {
                    throw new InvalidOperationException(
                        $"{previous.Name} bounds {previous.Rect} overlap {current.Name} bounds {current.Rect}.");
                }
            }
        }

        private static void AssertIdentity(
            TMP_Text target,
            TMP_FontAsset font,
            Material material,
            FontStyles style,
            string context)
        {
            if (!ReferenceEquals(target.font, font) ||
                !ReferenceEquals(target.fontSharedMaterial, material) ||
                target.fontStyle != style)
            {
                throw new InvalidOperationException($"{context} typography identity mismatch.");
            }
        }

        private static RectTransform GetTemplateRow(ObjectiveHudView view)
        {
            var field = typeof(ObjectiveHudView).GetField(
                "_objectiveItemTemplate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(view) as RectTransform
                   ?? throw new InvalidOperationException("Objective item template reference was not found.");
        }

        private static IReadOnlyList<ActiveRow> GetActiveRows(ObjectiveHudView view)
        {
            return view.GetComponentsInChildren<ObjectiveHudRowView>(true)
                .Where(row => row != null &&
                              row.gameObject.activeInHierarchy &&
                              !string.IsNullOrWhiteSpace(row.StableId))
                .Select(row => new ActiveRow(
                    row,
                    GetRowLabel(row)))
                .OrderBy(row => row.View.transform.GetSiblingIndex())
                .ToArray();
        }

        private static TMP_Text GetRowLabel(ObjectiveHudRowView row)
        {
            var field = typeof(ObjectiveHudRowView).GetField(
                "_label",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(row) as TMP_Text
                   ?? throw new InvalidOperationException("Objective row label reference was not found.");
        }

        private static AssetIdentity ReadIdentity(TMP_Text target)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                target.font,
                out var fontGuid,
                out long fontLocalId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                target.fontSharedMaterial,
                out var materialGuid,
                out long materialLocalId);
            return new AssetIdentity(
                target.font != null ? target.font.name : string.Empty,
                fontGuid,
                fontLocalId,
                target.fontSharedMaterial != null ? target.fontSharedMaterial.name : string.Empty,
                materialGuid,
                materialLocalId,
                target.fontStyle.ToString());
        }

        private static bool HasNonBlankPixels(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            return pixels.Length > 0 && pixels.Any(pixel => !pixel.Equals(pixels[0]));
        }

        private static string WriteManifest(
            string outputDirectory,
            int width,
            int height,
            string gitHead,
            IReadOnlyList<CaptureRecord> captures,
            IReadOnlyList<string> errors)
        {
            var builder = new StringBuilder();
            Append(builder, "schema_version", "1");
            Append(builder, "generated_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            Append(builder, "git_head", gitHead);
            Append(builder, "unity_version", UnityEngine.Application.unityVersion);
            Append(builder, "capture_command", CommandLineEntryPoint);
            Append(builder, "capture_mode", "OBJECTIVE_HUD_PRODUCTION_COMPOSITION");
            Append(builder, "resolution", $"{width}x{height}");
            Append(builder, "capture_count", captures.Count.ToString(CultureInfo.InvariantCulture));
            Append(builder, "errors", errors.Count.ToString(CultureInfo.InvariantCulture));
            Append(
                builder,
                "overall_result",
                errors.Count == 0 &&
                captures.Count == Scenarios.Length &&
                captures.All(capture => capture.Passed)
                    ? "PASS"
                    : "FAIL");
            Append(builder, "canonical_status", "CANDIDATE_PENDING_INDEPENDENT_AUDIT");

            foreach (var capture in captures)
            {
                builder.AppendLine();
                builder.Append('[')
                    .Append(capture.Scenario.State)
                    .Append('/')
                    .Append(capture.Scenario.Locale)
                    .AppendLine("]");
                Append(builder, "surface", "HUD Objectives");
                Append(builder, "state", capture.Scenario.State);
                Append(builder, "locale", capture.Scenario.Locale);
                Append(builder, "file", capture.FileName);
                Append(builder, "file_size_bytes", capture.FileSizeBytes.ToString(CultureInfo.InvariantCulture));
                Append(builder, "sha256", capture.Sha256);
                Append(builder, "dimensions", $"{width}x{height}");
                Append(builder, "decode", "PASS");
                Append(builder, "nonblank", capture.NonBlank);
                Append(builder, "glyph_coverage", capture.GlyphCoverage);
                Append(builder, "layout", capture.Layout);
                Append(builder, "header_font", capture.HeaderIdentity.FontName);
                Append(builder, "header_font_guid", capture.HeaderIdentity.FontGuid);
                Append(builder, "header_font_local_id", capture.HeaderIdentity.FontLocalId.ToString(CultureInfo.InvariantCulture));
                Append(builder, "header_material", capture.HeaderIdentity.MaterialName);
                Append(builder, "header_material_guid", capture.HeaderIdentity.MaterialGuid);
                Append(builder, "header_material_local_id", capture.HeaderIdentity.MaterialLocalId.ToString(CultureInfo.InvariantCulture));
                Append(builder, "header_style", capture.HeaderIdentity.Style);
                Append(builder, "row_font", capture.RowIdentity.FontName);
                Append(builder, "row_font_guid", capture.RowIdentity.FontGuid);
                Append(builder, "row_font_local_id", capture.RowIdentity.FontLocalId.ToString(CultureInfo.InvariantCulture));
                Append(builder, "row_material", capture.RowIdentity.MaterialName);
                Append(builder, "row_material_guid", capture.RowIdentity.MaterialGuid);
                Append(builder, "row_material_local_id", capture.RowIdentity.MaterialLocalId.ToString(CultureInfo.InvariantCulture));
                Append(builder, "row_style", capture.RowIdentity.Style);
                Append(builder, "capture_result", capture.Passed ? "PASS" : "FAIL");
                Append(builder, "capture_errors", capture.Error);
            }

            foreach (var error in errors)
            {
                builder.AppendLine();
                builder.Append("# ERROR: ").AppendLine(Sanitize(error));
            }

            var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
            File.WriteAllText(
                manifestPath,
                builder.ToString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return manifestPath;
        }

        private static void Append(StringBuilder builder, string key, string value)
        {
            builder.Append(key).Append('=').AppendLine(Sanitize(value));
        }

        private static string Sanitize(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(bytes))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static string ReadArgument(IReadOnlyList<string> args, string key)
        {
            for (var i = 0; i < args.Count - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private static int ReadPositiveInt(
            IReadOnlyList<string> args,
            string key,
            int defaultValue)
        {
            var value = ReadArgument(args, key);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
                   parsed > 0
                ? parsed
                : defaultValue;
        }

        private readonly struct CaptureScenario
        {
            public CaptureScenario(string state, string locale, int expectedRowCount)
            {
                State = state;
                Locale = locale;
                ExpectedRowCount = expectedRowCount;
            }

            public string State { get; }

            public string Locale { get; }

            public int ExpectedRowCount { get; }
        }

        private readonly struct ActiveRow
        {
            public ActiveRow(ObjectiveHudRowView view, TMP_Text label)
            {
                View = view;
                Label = label;
            }

            public ObjectiveHudRowView View { get; }

            public TMP_Text Label { get; }
        }

        private readonly struct PresentationRect
        {
            public PresentationRect(string name, RectTransform target)
            {
                Name = name;
                Target = target;
            }

            public string Name { get; }

            public RectTransform Target { get; }
        }

        private readonly struct ProjectedPresentationRect
        {
            public ProjectedPresentationRect(string name, Rect rect)
            {
                Name = name;
                Rect = rect;
            }

            public string Name { get; }

            public Rect Rect { get; }
        }

        private readonly struct AssetIdentity
        {
            public AssetIdentity(
                string fontName,
                string fontGuid,
                long fontLocalId,
                string materialName,
                string materialGuid,
                long materialLocalId,
                string style)
            {
                FontName = fontName;
                FontGuid = fontGuid;
                FontLocalId = fontLocalId;
                MaterialName = materialName;
                MaterialGuid = materialGuid;
                MaterialLocalId = materialLocalId;
                Style = style;
            }

            public string FontName { get; }

            public string FontGuid { get; }

            public long FontLocalId { get; }

            public string MaterialName { get; }

            public string MaterialGuid { get; }

            public long MaterialLocalId { get; }

            public string Style { get; }
        }

        private readonly struct CaptureRecord
        {
            public CaptureRecord(
                CaptureScenario scenario,
                string fileName,
                long fileSizeBytes,
                string sha256,
                AssetIdentity headerIdentity,
                AssetIdentity rowIdentity,
                string nonBlank,
                string glyphCoverage,
                string layout,
                string error)
            {
                Scenario = scenario;
                FileName = fileName;
                FileSizeBytes = fileSizeBytes;
                Sha256 = sha256;
                HeaderIdentity = headerIdentity;
                RowIdentity = rowIdentity;
                NonBlank = nonBlank;
                GlyphCoverage = glyphCoverage;
                Layout = layout;
                Error = error ?? string.Empty;
            }

            public CaptureScenario Scenario { get; }

            public string FileName { get; }

            public long FileSizeBytes { get; }

            public string Sha256 { get; }

            public AssetIdentity HeaderIdentity { get; }

            public AssetIdentity RowIdentity { get; }

            public string NonBlank { get; }

            public string GlyphCoverage { get; }

            public string Layout { get; }

            public string Error { get; }

            public bool Passed =>
                FileSizeBytes > 0 &&
                !string.IsNullOrWhiteSpace(Sha256) &&
                string.Equals(NonBlank, "PASS", StringComparison.Ordinal) &&
                string.Equals(GlyphCoverage, "PASS", StringComparison.Ordinal) &&
                string.Equals(Layout, "PASS", StringComparison.Ordinal) &&
                string.IsNullOrEmpty(Error);
        }

        private sealed class MemoryLocalePreferenceStore : IUiLocalePreferenceStore
        {
            private string localeCode;

            public MemoryLocalePreferenceStore(string localeCode)
            {
                this.localeCode = localeCode;
            }

            public bool TryLoad(out string value)
            {
                value = localeCode;
                return !string.IsNullOrWhiteSpace(value);
            }

            public void Save(string value)
            {
                localeCode = value;
            }
        }

        private sealed class EvidencePresentationSource : IGameplayUiPresentationSource
        {
            public EvidencePresentationSource(UIPresentationSnapshot snapshot)
            {
                CurrentSnapshot = snapshot;
            }

            public event Action<UIPresentationSnapshot> SnapshotChanged;
            public event Action<UITickEventBatch> TickEventsApplied;
            public event Action<LevelFailedScreenPayload> LevelFailedCommitted;

            public UIPresentationSnapshot CurrentSnapshot { get; private set; }

            public UITickEventBatch CurrentTickEvents => UITickEventBatch.Empty;

            public MinimalStageCompletionReadModel CurrentMinimalStageCompletion => null;

            public LevelFailedScreenPayload CurrentLevelFailed => null;

            public void UpdateUiGameplayInputBlocked(bool isUiGameplayInputBlocked)
            {
            }

            public void Publish(UIPresentationSnapshot snapshot)
            {
                CurrentSnapshot = snapshot;
                SnapshotChanged?.Invoke(snapshot);
            }
        }

        private sealed class AssetFileRestoreGuard : IDisposable
        {
            private readonly string path;
            private readonly byte[] bytes;

            private AssetFileRestoreGuard(string path)
            {
                this.path = path;
                bytes = File.ReadAllBytes(path);
            }

            public static AssetFileRestoreGuard Capture(string path)
            {
                return new AssetFileRestoreGuard(path);
            }

            public void Dispose()
            {
                if (!File.ReadAllBytes(path).SequenceEqual(bytes))
                {
                    File.WriteAllBytes(path, bytes);
                }

                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var asset in assets)
                {
                    EditorUtility.ClearDirty(asset);
                }
            }
        }
    }
}
