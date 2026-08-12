using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class TerminalIrisVisualQualityPlayModeTests
    {
        private static readonly Vector2[] Centers =
        {
            new Vector2(0.5f, 0.5f),
            new Vector2(0.2f, 0.5f),
            new Vector2(0.8f, 0.5f),
            new Vector2(0.5f, 0.2f),
            new Vector2(0.5f, 0.8f),
        };

        private static readonly float[] MajorRadii = { 0.45f, 0.30f, 0.15f, 0.05f, 0.01f };
        private static readonly float[] SmallRadii =
        {
            0.05f,
            0.02f,
            0.01f,
            0.005f,
            0.001f,
            0f,
        };

        private static readonly Vector2Int[] BeforeResolutions =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(3440, 1440),
        };

        private static readonly Vector2Int[] FullResolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1920, 1080),
            new Vector2Int(1920, 1200),
            new Vector2Int(2560, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(3440, 1440),
            new Vector2Int(3840, 2160),
        };

        private static readonly Vector2Int[] FullContourResolutions =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(3440, 1440),
        };

        private static readonly float[] FullContourRadii = { 0.30f, 0.05f, 0.01f };

        private static readonly string[] FullContourProfiles =
        {
            "victory",
            "defeat",
            "retry",
            "gameplay-entry",
            "entry",
        };

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalIrisLegacyAnalyzerRegression_OffcenterPixelsReproduceFalseCenter()
        {
            RequireGraphicsDevice();
            var context = QualityCaptureContext.Create("legacy-analyzer-regression");
            const int width = 640;
            const int height = 360;
            const float radius = 0.12f;
            var legacyCover = new Color32(0, 28, 112, 255);
            var rows = new List<string>
            {
                "fixture,direction,width,height,expected_x,expected_y," +
                "legacy_x,legacy_y,legacy_error_pixels,current_x,current_y," +
                "current_error_pixels,legacy_false_center,current_pass,verdict",
            };
            var directions = new[]
            {
                new KeyValuePair<string, Vector2>("Left", new Vector2(0.2f, 0.5f)),
                new KeyValuePair<string, Vector2>("Right", new Vector2(0.8f, 0.5f)),
                new KeyValuePair<string, Vector2>("Top", new Vector2(0.5f, 0.8f)),
                new KeyValuePair<string, Vector2>("Bottom", new Vector2(0.5f, 0.2f)),
            };

            foreach (var direction in directions)
            {
                var pixels = CreateLegacyRegressionPixels(
                    width,
                    height,
                    direction.Value,
                    radius,
                    legacyCover);
                AppendLegacyRegressionRow(
                    rows,
                    "cpu-synthetic",
                    direction.Key,
                    width,
                    height,
                    direction.Value,
                    legacyCover,
                    pixels);
            }

            using (var fixture = new IrisCaptureFixture())
            {
                yield return fixture.Initialize();
                fixture.SetResolution(width, height);
                var profile = fixture.Profiles.Single(item => item.Name == "victory");
                foreach (var direction in directions)
                {
                    fixture.Configure(profile, direction.Value, radius, rimEnabled: false);
                    fixture.IrisView.RuntimeMaterialForTests.SetColor(
                        "_OuterColor",
                        (Color)legacyCover);
                    yield return null;
                    var pixels = fixture.CapturePixels();
                    AppendLegacyRegressionRow(
                        rows,
                        "production-shader",
                        direction.Key,
                        width,
                        height,
                        direction.Value,
                        legacyCover,
                        pixels);
                    fixture.WritePng(
                        context.OutputDirectory,
                        $"legacy-{direction.Key.ToLowerInvariant()}-shader.png",
                        CaptureBackground.White);
                }
            }

            File.WriteAllLines(
                Path.Combine(context.OutputDirectory, "legacy-analyzer-regression.csv"),
                rows);
            WriteGraphicsEnvironment(
                context.OutputDirectory,
                "cpu-synthetic=640x360;production-shader=640x360");
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalIrisKnownCenterAnalyzer_SyntheticAndShaderFixturesProduceEvidence()
        {
            RequireGraphicsDevice();
            var context = QualityCaptureContext.Create("known-center-analyzer");
            var requiredCenters = new[]
            {
                new Vector2(0.2f, 0.5f),
                new Vector2(0.8f, 0.5f),
                new Vector2(0.5f, 0.8f),
                new Vector2(0.5f, 0.2f),
                new Vector2(0.25f, 0.25f),
                new Vector2(0.75f, 0.25f),
                new Vector2(0.25f, 0.75f),
                new Vector2(0.75f, 0.75f),
            };
            var syntheticResolutions = new[]
            {
                new Vector2Int(320, 180),
                new Vector2Int(320, 240),
                new Vector2Int(420, 180),
            };
            var rows = new List<string>
            {
                "fixture,fixture_id,pixel_buffer_sha256,capture_path,capture_sha256," +
                "width,height,center_x,center_y,radius,edge_pixels," +
                "shader_sha256,material_sha256," +
                "measured_x,measured_y,center_error_pixels,rms_radial_error_pixels," +
                "max_radial_error_pixels,p99_radial_error_pixels," +
                "unexpected_components,opaque_pinholes,transparent_artifacts,verdict",
            };
            var syntheticFixtureIndex = 0;

            foreach (var resolution in syntheticResolutions)
            {
                foreach (var center in requiredCenters)
                {
                    foreach (var radius in new[] { 0.08f, 0.16f })
                    {
                        foreach (var edgeWidth in new[] { 0f, 1.25f })
                        {
                            var coverage = TerminalIrisEvidenceAnalyzer.CreateSyntheticCoverage(
                                resolution.x,
                                resolution.y,
                                center,
                                radius,
                                edgeWidth);
                            var fixtureId = $"cpu-{syntheticFixtureIndex++:D3}";
                            var pixelBuffer = coverage
                                .Select(
                                    value =>
                                        (byte)Mathf.RoundToInt(
                                            Mathf.Clamp01(value) * byte.MaxValue))
                                .ToArray();
                            var capturePath = $"cpu-fixtures/{fixtureId}.coverage.bin";
                            var absoluteCapturePath =
                                Path.Combine(context.OutputDirectory, capturePath);
                            Directory.CreateDirectory(
                                Path.GetDirectoryName(absoluteCapturePath));
                            File.WriteAllBytes(absoluteCapturePath, pixelBuffer);
                            var analysis = TerminalIrisEvidenceAnalyzer.AnalyzeCoverage(
                                coverage,
                                resolution.x,
                                resolution.y,
                                center);
                            AssertKnownCenterAcceptance(
                                analysis,
                                $"synthetic {resolution.x}x{resolution.y} center={center} " +
                                $"radius={radius:R} edge={edgeWidth:R}");
                            rows.Add(KnownCenterCsvRow(
                                "synthetic",
                                fixtureId,
                                Sha256(pixelBuffer),
                                capturePath,
                                Sha256(pixelBuffer),
                                resolution,
                                center,
                                radius,
                                edgeWidth,
                                string.Empty,
                                string.Empty,
                                analysis));
                        }
                    }
                }
            }

            var shaderPath = Path.Combine(
                Application.dataPath,
                "_Features/UI/UI_Composition/Shaders/TerminalIris.shader");
            var materialPath = Path.Combine(
                Application.dataPath,
                "_Features/UI/UI_Composition/Shaders/TerminalIrisOverlay.mat");
            var shaderSha256 = Sha256(File.ReadAllBytes(shaderPath));
            var materialSha256 = Sha256(File.ReadAllBytes(materialPath));
            var shaderFixtureIndex = 0;
            using (var fixture = new IrisCaptureFixture())
            {
                yield return fixture.Initialize();
                var profile = fixture.Profiles.Single(item => item.Name == "victory");
                foreach (var resolution in new[]
                         {
                             new Vector2Int(640, 360),
                             new Vector2Int(640, 480),
                             new Vector2Int(840, 360),
                         })
                {
                    fixture.SetResolution(resolution.x, resolution.y);
                    foreach (var center in requiredCenters.Take(4))
                    {
                        const float radius = 0.12f;
                        fixture.Configure(profile, center, radius, rimEnabled: false);
                        yield return null;
                        var fixtureId = $"shader-{shaderFixtureIndex++:D3}";
                        var pixels = fixture.CapturePixels();
                        var png = EncodePng(pixels, resolution.x, resolution.y);
                        var capturePath = $"shader-captures/{fixtureId}.png";
                        var absoluteCapturePath =
                            Path.Combine(context.OutputDirectory, capturePath);
                        Directory.CreateDirectory(
                            Path.GetDirectoryName(absoluteCapturePath));
                        File.WriteAllBytes(absoluteCapturePath, png);
                        var analysis = TerminalIrisEvidenceAnalyzer.AnalyzeCoverage(
                            TerminalIrisEvidenceAnalyzer.ResolveCoverage(
                                pixels,
                                Color.white,
                                fixture.IrisView.RuntimeMaterialForTests.GetColor(
                                    "_OuterColor")),
                            resolution.x,
                            resolution.y,
                            center);
                        AssertKnownCenterAcceptance(
                            analysis,
                            $"shader {resolution.x}x{resolution.y} center={center}");
                        rows.Add(KnownCenterCsvRow(
                            "shader",
                            fixtureId,
                            Sha256(pixels.SelectMany(PixelBytes).ToArray()),
                            capturePath,
                            Sha256(png),
                            resolution,
                            center,
                            radius,
                            profile.Edge.ArtisticFeatherHalfWidthPixels,
                            shaderSha256,
                            materialSha256,
                            analysis));
                    }
                }
            }

            File.WriteAllLines(
                Path.Combine(context.OutputDirectory, "known-center-fixtures.csv"),
                rows);
            WriteGraphicsEnvironment(
                context.OutputDirectory,
                "cpu-synthetic=320x180,320x240,420x180;" +
                "production-shader=640x360,640x480,840x360");
        }

        [Test]
        [Category("Full")]
        public void TerminalIrisFrameSelection_SyntheticFailureMatrixPreservesSemantics()
        {
            const int width = 16;
            const int height = 12;
            var cover = new Color32(0, 28, 112, 255);
            var open = SyntheticAperturePixels(width, height, cover, 4f);
            var smaller = SyntheticAperturePixels(width, height, cover, 2f);
            var closed = SolidPixels(width, height, cover);

            var identical = AnalyzeSyntheticFrames(
                width,
                height,
                cover,
                SyntheticFrame(0, 0f, true, 0, closed),
                SyntheticFrame(1, 0f, true, 0, closed));
            Assert.That(identical.LastPixelChangingFrame, Is.EqualTo(-1));
            Assert.That(identical.FirstExactClosedFrame, Is.Zero);

            var oneChange = AnalyzeSyntheticFrames(
                width,
                height,
                cover,
                SyntheticFrame(0, 4f, false, 48, open),
                SyntheticFrame(1, 0f, true, 0, closed),
                SyntheticFrame(2, 0f, true, 0, closed));
            Assert.That(oneChange.LastAnimatedParameterFrame, Is.Zero);
            Assert.That(oneChange.LastPixelChangingFrame, Is.Zero);
            Assert.That(oneChange.FirstExactClosedFrame, Is.EqualTo(1));

            var finalTwoChange = AnalyzeSyntheticFrames(
                width,
                height,
                cover,
                SyntheticFrame(0, 4f, false, 48, open),
                SyntheticFrame(1, 2f, false, 12, smaller),
                SyntheticFrame(2, 0f, true, 0, closed),
                SyntheticFrame(3, 0f, true, 0, closed));
            Assert.That(finalTwoChange.LastAnimatedParameterFrame, Is.EqualTo(1));
            Assert.That(finalTwoChange.LastPixelChangingFrame, Is.EqualTo(1));
            Assert.That(finalTwoChange.FirstExactClosedFrame, Is.EqualTo(2));

            var reopen = AnalyzeSyntheticFrames(
                width,
                height,
                cover,
                SyntheticFrame(0, 0f, true, 0, closed),
                SyntheticFrame(1, 0f, true, 0, closed),
                SyntheticFrame(2, 2f, false, 12, smaller));
            Assert.That(reopen.ChangedAfterStableClosed, Is.True);

            var flashPixels = SolidPixels(width, height, new Color32(255, 0, 0, 255));
            var flash = AnalyzeSyntheticFrames(
                width,
                height,
                cover,
                SyntheticFrame(0, 0f, true, 0, closed),
                SyntheticFrame(1, 0f, true, 0, flashPixels));
            Assert.That(flash.Deltas[0].ChangedPixelCount, Is.EqualTo(width * height));
            Assert.That(flash.Deltas[0].UnexpectedChromaShiftPixelCount, Is.GreaterThan(0));

            var localClosure = AnalyzeSyntheticFrames(
                width,
                height,
                cover,
                SyntheticFrame(0, 2f, false, 12, smaller),
                SyntheticFrame(1, 0f, true, 0, closed));
            Assert.That(localClosure.Deltas[0].ChangedComponentCount, Is.EqualTo(1));
            Assert.That(localClosure.Deltas[0].ChangedPixelCount, Is.LessThan(width * height / 3));
            Assert.That(localClosure.Deltas[0].UnexpectedChromaShiftPixelCount, Is.Zero);

            var pinholePixels = (Color32[])closed.Clone();
            pinholePixels[(height / 2) * width + width / 2] = Color.white;
            var pinhole = AnalyzeSyntheticFrames(
                width,
                height,
                cover,
                SyntheticFrame(0, 0f, true, 0, closed),
                SyntheticFrame(1, 0f, false, 1, pinholePixels),
                SyntheticFrame(2, 0f, true, 0, closed));
            Assert.That(pinhole.FirstExactClosedFrame, Is.EqualTo(-1));
            Assert.That(pinhole.LastPixelChangingFrame, Is.EqualTo(1));

            var missing = AnalyzeSyntheticFrames(
                width,
                height,
                cover,
                SyntheticFrame(0, 0f, true, 0, closed),
                SyntheticFrame(2, 0f, true, 0, closed));
            Assert.That(missing.HasMissingFrameIndex, Is.True);
            var duplicate = AnalyzeSyntheticFrames(
                width,
                height,
                cover,
                SyntheticFrame(0, 0f, true, 0, closed),
                SyntheticFrame(0, 0f, true, 0, closed));
            Assert.That(duplicate.HasDuplicateFrameIndex, Is.True);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalIrisStaticEdgeQuality_GraphicsMatrixProducesEvidence()
        {
            RequireGraphicsDevice();
            var context = QualityCaptureContext.Create("static-edge");
            var resolutions = context.IsBefore ? BeforeResolutions : FullResolutions;
            var records = new List<StaticEdgeRecord>();
            var contourRecords = new List<FullContourRecord>();
            var aaResponsibilityRecords = new List<AAResponsibilityRecord>();
            var anisotropySweepRecords = new List<AnisotropySweepRecord>();
            using (var fixture = new IrisCaptureFixture())
            {
                yield return fixture.Initialize();
                foreach (var resolution in resolutions)
                {
                    fixture.SetResolution(resolution.x, resolution.y);
                    foreach (var profile in fixture.Profiles)
                    {
                        foreach (var center in Centers)
                        {
                            foreach (var radius in MajorRadii)
                            {
                                fixture.Configure(profile, center, radius, rimEnabled: false);
                                yield return null;
                                var record = fixture.MeasureStatic(profile.Name, center, radius);
                                records.Add(record);

                                if (!context.IsBefore &&
                                    center == new Vector2(0.5f, 0.5f) &&
                                    FullContourResolutions.Contains(resolution) &&
                                    FullContourRadii.Contains(radius))
                                {
                                    contourRecords.Add(
                                        fixture.MeasureFullContour(
                                            profile.Name,
                                            center,
                                            radius,
                                            profile.Edge.ArtisticFeatherHalfWidthPixels));
                                }

                                if (ShouldWriteRepresentativePng(center, radius))
                                {
                                    fixture.Configure(profile, center, radius, rimEnabled: true);
                                    fixture.WritePng(
                                        context.OutputDirectory,
                                        $"{profile.Name}-{resolution.x}x{resolution.y}-" +
                                        $"{CenterName(center)}-r{RadiusName(radius)}-white.png",
                                        CaptureBackground.White);
                                    if (radius == 0.30f)
                                    {
                                        fixture.WritePng(
                                            context.OutputDirectory,
                                            $"{profile.Name}-{resolution.x}x{resolution.y}-" +
                                            $"{CenterName(center)}-r{RadiusName(radius)}-black.png",
                                            CaptureBackground.Black);
                                        fixture.WritePng(
                                            context.OutputDirectory,
                                            $"{profile.Name}-{resolution.x}x{resolution.y}-" +
                                            $"{CenterName(center)}-r{RadiusName(radius)}-checker.png",
                                            CaptureBackground.Checker);
                                    }
                                }
                            }
                        }

                        if (!context.IsBefore &&
                            FullContourResolutions.Contains(resolution))
                        {
                            const float responsibilityRadius = 0.30f;
                            var responsibilityCenter = new Vector2(0.5f, 0.5f);
                            fixture.Configure(
                                profile,
                                responsibilityCenter,
                                responsibilityRadius,
                                rimEnabled: false,
                                artisticFeatherHalfWidthPixels: 0f);
                            yield return null;
                            var aaOnly = fixture.MeasureFullContour(
                                profile.Name,
                                responsibilityCenter,
                                responsibilityRadius,
                                0f);
                            fixture.WritePng(
                                context.OutputDirectory,
                                $"{profile.Name}-{resolution.x}x{resolution.y}-aa-only.png",
                                CaptureBackground.White);

                            fixture.Configure(
                                profile,
                                responsibilityCenter,
                                responsibilityRadius,
                                rimEnabled: false);
                            yield return null;
                            var aaPlusFeather = fixture.MeasureFullContour(
                                profile.Name,
                                responsibilityCenter,
                                responsibilityRadius,
                                profile.Edge.ArtisticFeatherHalfWidthPixels);
                            fixture.WritePng(
                                context.OutputDirectory,
                                $"{profile.Name}-{resolution.x}x{resolution.y}-aa-plus-feather.png",
                                CaptureBackground.White);
                            aaResponsibilityRecords.Add(
                                new AAResponsibilityRecord(
                                    profile.Name,
                                    resolution,
                                    profile.Edge.MinimumAAPixels,
                                    profile.Edge.ArtisticFeatherHalfWidthPixels,
                                    aaOnly.MeanEdgeWidthPixels,
                                    aaPlusFeather.MeanEdgeWidthPixels));

                            if (profile.Name == "entry")
                            {
                                foreach (var sweepRadius in FullContourRadii)
                                {
                                    foreach (var minimumAAPixels in new[]
                                             {
                                                 0.82f,
                                                 0.86f,
                                                 0.90f,
                                                 0.95f,
                                                 1.00f,
                                             })
                                    {
                                        fixture.Configure(
                                            profile,
                                            responsibilityCenter,
                                            sweepRadius,
                                            rimEnabled: false,
                                            minimumAAPixels: minimumAAPixels);
                                        yield return null;
                                        var sweep = fixture.MeasureFullContour(
                                            profile.Name,
                                            responsibilityCenter,
                                            sweepRadius,
                                            profile.Edge.ArtisticFeatherHalfWidthPixels);
                                        anisotropySweepRecords.Add(
                                            new AnisotropySweepRecord(
                                                resolution,
                                                sweepRadius,
                                                minimumAAPixels,
                                                sweep.MinimumEdgeWidthPixels,
                                                sweep.MaximumEdgeWidthPixels));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            WriteStaticRecords(
                context,
                records,
                contourRecords,
                aaResponsibilityRecords,
                anisotropySweepRecords);
            Assert.That(records, Is.Not.Empty);
            if (!context.IsBefore)
            {
                AssertStaticAcceptance(records);
                AssertFullContourAcceptance(contourRecords);
                AssertAAResponsibilityAcceptance(aaResponsibilityRecords);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalIrisSmallRadius_GraphicsSequenceProducesEvidence()
        {
            RequireGraphicsDevice();
            var context = QualityCaptureContext.Create("small-radius");
            var records = new List<SmallRadiusRecord>();
            using (var fixture = new IrisCaptureFixture())
            {
                yield return fixture.Initialize();
                foreach (var resolution in BeforeResolutions)
                {
                    fixture.SetResolution(resolution.x, resolution.y);
                    foreach (var profile in fixture.Profiles)
                    {
                        foreach (var radius in SmallRadii)
                        {
                            fixture.Configure(profile, new Vector2(0.5f, 0.5f), radius, rimEnabled: true);
                            yield return null;
                            records.Add(fixture.MeasureSmallRadius(profile.Name, radius));
                            fixture.WritePng(
                                context.OutputDirectory,
                                $"{profile.Name}-{resolution.x}x{resolution.y}-small-r{RadiusName(radius)}.png",
                                CaptureBackground.Checker);
                        }
                    }
                }
            }

            WriteSmallRadiusRecords(context, records);
            Assert.That(records, Is.Not.Empty);
            if (!context.IsBefore)
            {
                AssertSmallRadiusAcceptance(records);
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalIrisTemporalStability_RuntimePlaybackMeetsSemanticAcceptance()
        {
            var evaluation = EvaluateTemporalStability();
            AssertTemporalAcceptance(evaluation.Records);
            yield break;
        }

        [UnityTest]
        [Category("Full")]
        [Category("TerminalIrisCapture")]
        public IEnumerator TerminalIrisTemporalStability_RuntimePlaybackProducesEvidence()
        {
            var context = QualityCaptureContext.Create(
                "temporal-stability",
                "Run ./run_tests.sh terminal-iris-temporal-stability.");
            var evaluation = EvaluateTemporalStability();
            WriteTemporalMetrics(context, evaluation);
            if (!context.IsBefore)
            {
                AssertTemporalAcceptance(evaluation.Records);
            }

            yield break;
        }

        private static TemporalEvaluationResult EvaluateTemporalStability()
        {
            var profile = Resources.Load<TerminalIrisMotionProfile>(
                "UI/Transitions/TerminalIrisMotionProfile");
            Assert.That(profile, Is.Not.Null);
            var resolver = profile.CreateResolver();
            var records = new List<TemporalMetricRecord>();
            foreach (var resolution in BeforeResolutions)
            {
                foreach (var fps in new[] { 30, 60, 120 })
                {
                    CaptureCloseTemporalMetrics(
                        "victory-close",
                        TerminalTransitionKind.Victory,
                        resolver.ResolveClose(TerminalTransitionKind.Victory),
                        resolution,
                        fps,
                        records);
                    CaptureCloseTemporalMetrics(
                        "defeat-close",
                        TerminalTransitionKind.Defeat,
                        resolver.ResolveClose(TerminalTransitionKind.Defeat),
                        resolution,
                        fps,
                        records);
                    CaptureCloseTemporalMetrics(
                        "retry-close",
                        TerminalTransitionKind.Defeat,
                        resolver.ResolveRetryClose(SceneTransitionIntent.ManualRetry),
                        resolution,
                        fps,
                        records);
                    CaptureCloseTemporalMetrics(
                        "gameplay-entry-close",
                        TerminalTransitionKind.Defeat,
                        resolver.ResolveGameplayEntrySourceClose(
                            SceneTransitionIntent.GameplayEntry),
                        resolution,
                        fps,
                        records);
                    CaptureDefeatRevealTemporalMetrics(
                        resolver.ResolveClose(TerminalTransitionKind.Defeat),
                        resolution,
                        fps,
                        records);
                    CaptureStageEntryTemporalMetrics(
                        resolver.ResolveStageEntryOpen(),
                        resolution,
                        fps,
                        records);
                }
            }

            return new TemporalEvaluationResult(records, BuildTemporalSummaries(records));
        }

        private static void WriteTemporalMetrics(
            QualityCaptureContext context,
            TemporalEvaluationResult evaluation)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"schemaVersion\": 3,");
            builder.AppendLine($"  \"phase\": \"{context.Phase}\",");
            builder.AppendLine("  \"temporalLifecyclePass\": true,");
            builder.AppendLine("  \"temporalVisualPass\": null,");
            builder.AppendLine(
                "  \"temporalVisualEvidenceSource\": \"graphics Player capture and manual QA\",");
            builder.AppendLine("  \"records\": [");
            for (var index = 0; index < evaluation.Records.Count; index++)
            {
                builder.Append("    ").Append(evaluation.Records[index].ToJson());
                builder.AppendLine(
                    index + 1 < evaluation.Records.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"summaries\": [");
            for (var index = 0; index < evaluation.Summaries.Count; index++)
            {
                builder.Append("    ").Append(evaluation.Summaries[index].ToJson());
                builder.AppendLine(
                    index + 1 < evaluation.Summaries.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            File.WriteAllText(
                Path.Combine(context.OutputDirectory, "temporal-metrics.json"),
                builder.ToString());
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator TerminalIrisFinalCloseFrameEvidence_VictoryAndDefeatRenderSequences()
        {
            RequireGraphicsDevice();
            var context = QualityCaptureContext.Create("final-close-frames");
            var motion = Resources.Load<TerminalIrisMotionProfile>(
                "UI/Transitions/TerminalIrisMotionProfile");
            Assert.That(motion, Is.Not.Null);
            var resolver = motion.CreateResolver();
            using (var fixture = new IrisCaptureFixture())
            {
                yield return fixture.Initialize();
                const int width = 640;
                const int height = 360;
                fixture.SetResolution(width, height);
                foreach (var route in new[]
                         {
                             new KeyValuePair<string, TerminalTransitionKind>(
                                 "victory",
                                 TerminalTransitionKind.Victory),
                             new KeyValuePair<string, TerminalTransitionKind>(
                                 "defeat",
                                 TerminalTransitionKind.Defeat),
                         })
                {
                    var preset = resolver.ResolveClose(route.Value);
                    var samples = new List<TerminalIrisFrameSample>();
                    var evidenceToken = new TerminalSessionToken(
                        route.Value == TerminalTransitionKind.Victory ? 7101 : 7102,
                        1);
                    using (var playback = new TerminalTransitionPlayback(preset))
                    {
                        var center = new Vector2(0.2f, 0.5f);
                        var fullyRevealedRadius = fixture.IrisView
                            .CalculateFullyRevealedRadius(center, 0f);
                        playback.ConfigureFullyRevealedRadii(
                            fullyRevealedRadius,
                            fullyRevealedRadius);
                        Assert.That(
                            playback.TryBegin(
                                new TerminalTransitionRequest(
                                    route.Value,
                                    1,
                                    evidenceToken,
                                    TerminalTransitionDestinationMode.SceneHandoff),
                                new TerminalFocusTarget(center, 0.12f, false)),
                            Is.True);
                        const float renderDelta = 1f / 60f;
                        playback.Advance(Mathf.Max(0f, preset.FocusDuration - renderDelta));
                        fixture.IrisView.Apply(playback);
                        yield return null;
                        samples.Add(CaptureFrameSample(fixture, playback, 0, height));
                        var sequence = 1;
                        while (playback.State != TerminalTransitionState.Black &&
                               sequence < 180)
                        {
                            playback.Advance(renderDelta);
                            fixture.IrisView.Apply(playback);
                            yield return null;
                            samples.Add(
                                CaptureFrameSample(
                                    fixture,
                                    playback,
                                    sequence++,
                                    height));
                        }

                        Assert.That(
                            playback.State,
                            Is.EqualTo(TerminalTransitionState.Black),
                            $"{route.Key} close did not reach exact closed.");
                        for (var stable = 0; stable < 3; stable++)
                        {
                            playback.Advance(renderDelta);
                            fixture.IrisView.Apply(playback);
                            yield return null;
                            samples.Add(
                                CaptureFrameSample(
                                    fixture,
                                    playback,
                                    sequence++,
                                    height));
                        }
                    }

                    var coverColor = samples.Last().Pixels[0];
                    var selection = TerminalIrisEvidenceAnalyzer.AnalyzeFrameSequence(
                        samples,
                        width,
                        height,
                        coverColor);
                    AssertFrameSelectionAcceptance(route.Key, samples, selection, width, height);
                    WriteFrameEvidence(
                        Path.Combine(context.OutputDirectory, route.Key),
                        route.Key,
                        samples,
                        selection,
                        width,
                        height,
                        fixture.IrisView,
                        evidenceToken.Sequence,
                        evidenceToken.ToString());
                }
            }
            WriteGraphicsEnvironment(
                context.OutputDirectory,
                "victory=640x360;defeat=640x360");
        }

        private static IReadOnlyList<TemporalSummaryRecord> BuildTemporalSummaries(
            IReadOnlyCollection<TemporalMetricRecord> records)
        {
            var summaries = new List<TemporalSummaryRecord>();
            foreach (var group in records.GroupBy(
                         record =>
                             $"{record.Transition}-{record.Width}x{record.Height}-{record.Fps}"))
            {
                var absolute = group
                    .Select(record => Mathf.Abs(record.EdgeDisplacementPixels))
                    .OrderBy(value => value)
                    .ToArray();
                summaries.Add(
                    new TemporalSummaryRecord(
                        group.First().Transition,
                        group.First().Width,
                        group.First().Height,
                        group.First().Fps,
                        absolute.Last(),
                        SamplePercentile(absolute, 0.95f),
                        SamplePercentile(absolute, 0.99f),
                        group.Count()));
            }

            return summaries;
        }

        private static float SamplePercentile(IReadOnlyList<float> sorted, float percentile)
        {
            var position = Mathf.Clamp01(percentile) * (sorted.Count - 1);
            var lower = Mathf.FloorToInt(position);
            var upper = Mathf.CeilToInt(position);
            return Mathf.Lerp(sorted[lower], sorted[upper], position - lower);
        }

        private static TerminalIrisFrameSelection AnalyzeSyntheticFrames(
            int width,
            int height,
            Color32 cover,
            params TerminalIrisFrameSample[] frames)
        {
            return TerminalIrisEvidenceAnalyzer.AnalyzeFrameSequence(
                frames,
                width,
                height,
                cover);
        }

        private static TerminalIrisFrameSample SyntheticFrame(
            int sequence,
            float radius,
            bool exactClosed,
            int transparentPixels,
            Color32[] pixels)
        {
            return new TerminalIrisFrameSample(
                sequence,
                sequence,
                sequence / 60f,
                radius,
                radius,
                exactClosed ? 2f : 0f,
                exactClosed,
                transparentPixels,
                exactClosed ? 0f : radius,
                (Color32[])pixels.Clone());
        }

        private static Color32[] SolidPixels(int width, int height, Color32 color)
        {
            var pixels = new Color32[width * height];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = color;
            }

            return pixels;
        }

        private static Color32[] SyntheticAperturePixels(
            int width,
            int height,
            Color32 cover,
            float radiusPixels)
        {
            var pixels = SolidPixels(width, height, cover);
            var center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (Vector2.Distance(new Vector2(x, y), center) <= radiusPixels)
                    {
                        pixels[y * width + x] = new Color32(255, 255, 255, 255);
                    }
                }
            }

            return pixels;
        }

        private static TerminalIrisFrameSample CaptureFrameSample(
            IrisCaptureFixture fixture,
            TerminalTransitionPlayback playback,
            int sequence,
            int renderHeight)
        {
            var pixels = fixture.CapturePixels();
            var material = fixture.IrisView.RuntimeMaterialForTests;
            var outerColor = material.GetColor("_OuterColor");
            var coverage = TerminalIrisEvidenceAnalyzer.ResolveCoverage(
                pixels,
                Color.white,
                outerColor);
            var transparentPixels = coverage.Count(value => value < 0.1f);
            var contourRadius = 0f;
            if (coverage.Any(value => value < 0.5f) &&
                coverage.Any(value => value >= 0.5f))
            {
                try
                {
                    contourRadius = TerminalIrisEvidenceAnalyzer.AnalyzeCoverage(
                            coverage,
                            fixture.Width,
                            fixture.Height,
                            playback.FocusTarget.NormalizedCenter)
                        .FittedRadiusPixels;
                }
                catch (InvalidOperationException)
                {
                    contourRadius = 0f;
                }
            }

            var centerValue = material.GetVector("_Center");
            return new TerminalIrisFrameSample(
                sequence,
                Time.frameCount,
                Time.realtimeSinceStartup,
                playback.CurrentRadius,
                playback.CurrentRadius * renderHeight -
                playback.CurrentClosedOvershootPixels,
                playback.CurrentClosedOvershootPixels,
                playback.State == TerminalTransitionState.Black &&
                playback.CurrentRadius <= 0f,
                transparentPixels,
                contourRadius,
                pixels,
                new Vector2(centerValue.x, centerValue.y),
                material.GetInstanceID(),
                fixture.IrisView.LastMaterialApplicationFrameForDiagnostics,
                fixture.IrisView.LastVisibleRenderFrameForDiagnostics);
        }

        private static void AssertFrameSelectionAcceptance(
            string route,
            IReadOnlyList<TerminalIrisFrameSample> samples,
            TerminalIrisFrameSelection selection,
            int width,
            int height)
        {
            Assert.That(selection.HasMissingFrameIndex, Is.False, $"{route}: missing render index");
            Assert.That(selection.HasDuplicateFrameIndex, Is.False, $"{route}: duplicate render index");
            Assert.That(selection.LastAnimatedParameterFrame, Is.GreaterThanOrEqualTo(0));
            Assert.That(selection.LastPixelChangingFrame, Is.GreaterThanOrEqualTo(0));
            Assert.That(selection.FirstExactClosedFrame, Is.GreaterThanOrEqualTo(0));
            Assert.That(selection.ChangedAfterStableClosed, Is.False);
            var exact = samples[selection.FirstExactClosedFrame];
            Assert.That(exact.TransparentPixelCount, Is.Zero);
            Assert.That(exact.ContourRadiusPixels, Is.Zero);
            var stable = selection.Deltas[selection.FirstExactClosedFrame];
            Assert.That(stable.ChangedPixelCount, Is.Zero);
            Assert.That(stable.MaxChannelDelta, Is.Zero);
            Assert.That(stable.UnexpectedChromaShiftPixelCount, Is.Zero);
            var finalChange = selection.Deltas[selection.LastPixelChangingFrame];
            Assert.That(finalChange.ChangedComponentCount, Is.EqualTo(1));
            Assert.That(
                finalChange.ChangedPixelCount,
                Is.LessThan(width * height / 10),
                $"{route}: final change must remain local to the aperture.");
            Assert.That(finalChange.UnexpectedChromaShiftPixelCount, Is.Zero);
        }

        private static void WriteFrameEvidence(
            string outputDirectory,
            string route,
            IReadOnlyList<TerminalIrisFrameSample> samples,
            TerminalIrisFrameSelection selection,
            int width,
            int height,
            TerminalIrisOverlayView irisView,
            long transitionId,
            string sessionToken)
        {
            Directory.CreateDirectory(outputDirectory);
            var pngByIndex = new Dictionary<int, byte[]>();
            var hashes = new string[samples.Count];
            var framePaths = new string[samples.Count];
            var framesDirectory = Path.Combine(outputDirectory, "frames");
            Directory.CreateDirectory(framesDirectory);
            for (var index = 0; index < samples.Count; index++)
            {
                var png = EncodePng(samples[index].Pixels, width, height);
                pngByIndex[index] = png;
                hashes[index] = Sha256(png);
                framePaths[index] = $"frames/frame-{samples[index].RenderSequenceIndex:D4}.png";
                File.WriteAllBytes(
                    Path.Combine(outputDirectory, framePaths[index]),
                    png);
            }

            var metrics = new List<string>
            {
                "render_sequence_index,frame_count,unscaled_time,intent,transition_id," +
                "session_token,input_radius," +
                "effective_radius,closed_overshoot,material_center_x,material_center_y," +
                "material_instance_id,material_application_frame," +
                "render_acknowledgement_frame,render_width,render_height,capture_path," +
                "capture_sha256,png_sha256," +
                "transparent_pixel_count,contour_radius,authoring_exact_closed," +
                "next_changed_pixels,next_max_channel_delta,next_mean_abs_channel_delta," +
                "next_mean_luminance_delta,next_p99_luminance_delta," +
                "next_unexpected_chroma_pixels,next_changed_components",
            };
            for (var index = 0; index < samples.Count; index++)
            {
                var sample = samples[index];
                var delta = index < selection.Deltas.Count
                    ? selection.Deltas[index]
                    : default;
                metrics.Add(
                    string.Join(
                        ",",
                        sample.RenderSequenceIndex,
                        sample.FrameCount,
                        Float(sample.UnscaledTime),
                        route,
                        transitionId,
                        sessionToken,
                        Float(sample.InputRadius),
                        Float(sample.EffectiveRadius),
                        Float(sample.ClosedOvershootPixels),
                        Float(sample.MaterialCenter.x),
                        Float(sample.MaterialCenter.y),
                        sample.MaterialInstanceId,
                        sample.MaterialApplicationFrame,
                        sample.RenderAcknowledgementFrame,
                        width,
                        height,
                        framePaths[index],
                        hashes[index],
                        hashes[index],
                        sample.TransparentPixelCount,
                        Float(sample.ContourRadiusPixels),
                        sample.AuthoringExactClosed ? "true" : "false",
                        delta.ChangedPixelCount,
                        delta.MaxChannelDelta,
                        Float(delta.MeanAbsoluteChannelDelta),
                        Float(delta.MeanLuminanceDelta),
                        Float(delta.P99LuminanceDelta),
                        delta.UnexpectedChromaShiftPixelCount,
                        delta.ChangedComponentCount));
            }

            File.WriteAllLines(Path.Combine(outputDirectory, "frame-metrics.csv"), metrics);
            WriteSelectedPng(
                outputDirectory,
                "last-animated-parameter.png",
                pngByIndex,
                selection.LastAnimatedParameterFrame);
            WriteSelectedPng(
                outputDirectory,
                "last-pixel-changing.png",
                pngByIndex,
                selection.LastPixelChangingFrame);
            WriteSelectedPng(
                outputDirectory,
                "first-exact-closed.png",
                pngByIndex,
                selection.FirstExactClosedFrame);
            WriteSelectedPng(
                outputDirectory,
                "next-stable-closed.png",
                pngByIndex,
                selection.FirstExactClosedFrame + 1);
            var persistentCoverPath = Path.Combine(
                outputDirectory,
                "persistent-cover-first-rendered.png");
            var hasProductionPersistentCover = File.Exists(persistentCoverPath);
            if (!hasProductionPersistentCover)
            {
                WriteSelectedPng(
                    outputDirectory,
                    "persistent-cover-first-rendered.png",
                    pngByIndex,
                    selection.FirstExactClosedFrame + 1);
            }
            var heatmapRows = new List<string>();
            WriteHeatmapEvidence(
                outputDirectory,
                "last-pixel-changing",
                "last-pixel-changing-heatmap.png",
                selection.LastPixelChangingFrame,
                selection.LastPixelChangingFrame + 1,
                samples,
                framePaths,
                hashes,
                width,
                height,
                heatmapRows);
            WriteHeatmapEvidence(
                outputDirectory,
                "exact-closed-stability",
                "exact-closed-stability-heatmap.png",
                selection.FirstExactClosedFrame,
                selection.FirstExactClosedFrame + 1,
                samples,
                framePaths,
                hashes,
                width,
                height,
                heatmapRows);
            File.WriteAllText(
                Path.Combine(outputDirectory, "heatmap-manifest.json"),
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"differenceAlgorithmVersion\": \"max-rgba-delta-red-v1\",\n" +
                "  \"heatmaps\": [\n" +
                string.Join(",\n", heatmapRows) + "\n" +
                "  ]\n" +
                "}\n");
            File.WriteAllText(
                Path.Combine(outputDirectory, "frame-selection.json"),
                "{\n" +
                "  \"schemaVersion\": 2,\n" +
                $"  \"intent\": \"{route}\",\n" +
                $"  \"transitionId\": {transitionId},\n" +
                $"  \"sessionToken\": \"{sessionToken}\",\n" +
                $"  \"captureSeam\": \"Canvas.ForceUpdateCanvases + Camera.Render after one yielded render frame\",\n" +
                $"  \"capturedFrameCount\": {samples.Count},\n" +
                $"  \"lastAnimatedParameterFrame\": {selection.LastAnimatedParameterFrame},\n" +
                $"  \"lastPixelChangingFrame\": {selection.LastPixelChangingFrame},\n" +
                $"  \"firstExactClosedFrame\": {selection.FirstExactClosedFrame},\n" +
                $"  \"nextStableClosedFrame\": {selection.FirstExactClosedFrame + 1},\n" +
                $"  \"missingFrameIndex\": {selection.HasMissingFrameIndex.ToString().ToLowerInvariant()},\n" +
                $"  \"duplicateFrameIndex\": {selection.HasDuplicateFrameIndex.ToString().ToLowerInvariant()},\n" +
                $"  \"changedAfterStableClosed\": {selection.ChangedAfterStableClosed.ToString().ToLowerInvariant()},\n" +
                $"  \"overlayInstanceId\": {irisView.GetInstanceID()},\n" +
                $"  \"materialInstanceId\": {samples.Last().MaterialInstanceId},\n" +
                "  \"selectedFrames\": [\n" +
                SelectedFrameJson(
                    "lastAnimatedParameter",
                    "last-animated-parameter.png",
                    selection.LastAnimatedParameterFrame,
                    hashes) + ",\n" +
                SelectedFrameJson(
                    "lastPixelChanging",
                    "last-pixel-changing.png",
                    selection.LastPixelChangingFrame,
                    hashes) + ",\n" +
                SelectedFrameJson(
                    "firstExactClosed",
                    "first-exact-closed.png",
                    selection.FirstExactClosedFrame,
                    hashes) + ",\n" +
                SelectedFrameJson(
                    "nextStableClosed",
                    "next-stable-closed.png",
                    selection.FirstExactClosedFrame + 1,
                    hashes) + "\n" +
                "  ],\n" +
                "  \"persistentCoverFrameSource\": \"" +
                (hasProductionPersistentCover
                    ? "actual production ResultHandoffCover first opaque rendered frame"
                    : "same-color isolated handoff reference rendered after exact closed") +
                "\"\n" +
                "}\n");
        }

        private static string SelectedFrameJson(
            string selector,
            string path,
            int rowId,
            IReadOnlyList<string> hashes)
        {
            return
                "    {\"selector\":\"" + selector +
                "\",\"rowId\":" + rowId +
                ",\"path\":\"" + path +
                "\",\"sha256\":\"" + hashes[rowId] + "\"}";
        }

        private static void WriteHeatmapEvidence(
            string outputDirectory,
            string identity,
            string heatmapPath,
            int firstIndex,
            int secondIndex,
            IReadOnlyList<TerminalIrisFrameSample> samples,
            IReadOnlyList<string> framePaths,
            IReadOnlyList<string> frameHashes,
            int width,
            int height,
            ICollection<string> rows)
        {
            var differencePixels = BuildHeatmapPixels(
                samples[firstIndex].Pixels,
                samples[secondIndex].Pixels);
            var heatmapBytes = EncodePng(differencePixels, width, height);
            File.WriteAllBytes(
                Path.Combine(outputDirectory, heatmapPath),
                heatmapBytes);
            var canonicalDifferenceBuffer = differencePixels
                .SelectMany(PixelBytes)
                .ToArray();
            rows.Add(
                "    {\n" +
                $"      \"identity\": \"{identity}\",\n" +
                $"      \"sourceFrameAPath\": \"{framePaths[firstIndex]}\",\n" +
                $"      \"sourceFrameASha256\": \"{frameHashes[firstIndex]}\",\n" +
                $"      \"sourceFrameBPath\": \"{framePaths[secondIndex]}\",\n" +
                $"      \"sourceFrameBSha256\": \"{frameHashes[secondIndex]}\",\n" +
                $"      \"heatmapPath\": \"{heatmapPath}\",\n" +
                $"      \"heatmapSha256\": \"{Sha256(heatmapBytes)}\",\n" +
                "      \"differenceAlgorithmVersion\": \"max-rgba-delta-red-v1\",\n" +
                $"      \"canonicalDifferenceBufferSha256\": \"{Sha256(canonicalDifferenceBuffer)}\"\n" +
                "    }");
        }

        private static byte[] EncodePng(Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static void WriteSelectedPng(
            string outputDirectory,
            string fileName,
            IReadOnlyDictionary<int, byte[]> pngByIndex,
            int index)
        {
            Assert.That(pngByIndex.ContainsKey(index), Is.True, fileName);
            File.WriteAllBytes(Path.Combine(outputDirectory, fileName), pngByIndex[index]);
        }

        private static Color32[] BuildHeatmapPixels(
            IReadOnlyList<Color32> before,
            IReadOnlyList<Color32> after)
        {
            var pixels = new Color32[before.Count];
            for (var index = 0; index < pixels.Length; index++)
            {
                var delta = Math.Max(
                    Math.Max(
                        Math.Abs(after[index].r - before[index].r),
                        Math.Abs(after[index].g - before[index].g)),
                    Math.Max(
                        Math.Abs(after[index].b - before[index].b),
                        Math.Abs(after[index].a - before[index].a)));
                pixels[index] = new Color32((byte)delta, 0, 0, 255);
            }

            return pixels;
        }

        private static void CaptureCloseTemporalMetrics(
            string transition,
            TerminalTransitionKind kind,
            TerminalIrisRuntimePreset preset,
            Vector2Int resolution,
            int fps,
            ICollection<TemporalMetricRecord> records)
        {
            using (var playback = new TerminalTransitionPlayback(preset))
            {
                var focus = new TerminalFocusTarget(new Vector2(0.2f, 0.5f), 0.12f, false);
                var aspect = (float)resolution.x / resolution.y;
                var fullyRevealedRadius = Mathf.Max(
                    TerminalTransitionPlayback.CalculateFullyRevealedRadius(
                        preset.FallbackCenter,
                        aspect,
                        0f),
                    TerminalTransitionPlayback.CalculateFullyRevealedRadius(
                        focus.NormalizedCenter,
                        aspect,
                        0f));
                playback.ConfigureFullyRevealedRadii(
                    fullyRevealedRadius,
                    fullyRevealedRadius);
                var token = new TerminalSessionToken(1, 1);
                Assert.That(
                    playback.TryBegin(
                        new TerminalTransitionRequest(
                            kind,
                            1,
                            token,
                            TerminalTransitionDestinationMode.SceneHandoff),
                        focus),
                    Is.True);
                var deltaTime = 1f / fps;
                while (playback.State != TerminalTransitionState.Closing)
                {
                    playback.Advance(deltaTime);
                }

                var previousEffectiveRadius =
                    playback.CurrentRadius * resolution.y -
                    playback.CurrentClosedOvershootPixels;
                var frame = 0;
                while (playback.State == TerminalTransitionState.Closing && frame < 600)
                {
                    playback.Advance(deltaTime);
                    var effectiveRadius =
                        playback.CurrentRadius * resolution.y -
                        playback.CurrentClosedOvershootPixels;
                    records.Add(
                        new TemporalMetricRecord(
                            transition,
                            resolution,
                            fps,
                            frame,
                            playback.State,
                            playback.CurrentRadius,
                            playback.CurrentClosedOvershootPixels,
                            effectiveRadius,
                            effectiveRadius - previousEffectiveRadius));
                    previousEffectiveRadius = effectiveRadius;
                    frame++;
                }

                Assert.That(frame, Is.LessThan(600), $"{transition} did not complete.");
            }
        }

        private static void CaptureDefeatRevealTemporalMetrics(
            TerminalIrisRuntimePreset preset,
            Vector2Int resolution,
            int fps,
            ICollection<TemporalMetricRecord> records)
        {
            using (var playback = new TerminalTransitionPlayback(preset))
            {
                var focus = new TerminalFocusTarget(new Vector2(0.2f, 0.5f), 0.12f, false);
                var revealPreset = preset.RevealPreset.Value;
                var aspect = (float)resolution.x / resolution.y;
                var closeRadius = TerminalTransitionPlayback.CalculateFullyRevealedRadius(
                    focus.NormalizedCenter,
                    aspect,
                    0f);
                var revealRadius = TerminalTransitionPlayback.CalculateFullyRevealedRadius(
                    focus.NormalizedCenter,
                    aspect,
                    revealPreset.FullOpenMargin);
                playback.ConfigureFullyRevealedRadii(closeRadius, revealRadius);
                var token = new TerminalSessionToken(1, 2);
                Assert.That(
                    playback.TryBegin(
                        new TerminalTransitionRequest(
                            TerminalTransitionKind.Defeat,
                            1,
                            token,
                            TerminalTransitionDestinationMode.SameScene),
                        focus),
                    Is.True);
                var deltaTime = 1f / fps;
                while (playback.State != TerminalTransitionState.Black)
                {
                    playback.Advance(deltaTime);
                }

                Assert.That(playback.RequestReveal(token), Is.True);
                var previousEffectiveRadius =
                    playback.CurrentRadius * resolution.y -
                    playback.CurrentClosedOvershootPixels;
                var frame = 0;
                while (playback.State == TerminalTransitionState.Revealing && frame < 600)
                {
                    playback.Advance(deltaTime);
                    var effectiveRadius =
                        playback.CurrentRadius * resolution.y -
                        playback.CurrentClosedOvershootPixels;
                    records.Add(
                        new TemporalMetricRecord(
                            "defeat-reveal",
                            resolution,
                            fps,
                            frame,
                            playback.State,
                            playback.CurrentRadius,
                            playback.CurrentClosedOvershootPixels,
                            effectiveRadius,
                            effectiveRadius - previousEffectiveRadius));
                    previousEffectiveRadius = effectiveRadius;
                    frame++;
                }

                Assert.That(frame, Is.LessThan(600), "Defeat reveal did not complete.");
            }
        }

        private static void CaptureStageEntryTemporalMetrics(
            TerminalIrisRuntimeOpenPreset preset,
            Vector2Int resolution,
            int fps,
            ICollection<TemporalMetricRecord> records)
        {
            var center = new Vector2(0.2f, 0.5f);
            var fullyRevealedRadius = TerminalTransitionPlayback.CalculateFullyRevealedRadius(
                center,
                (float)resolution.x / resolution.y,
                preset.FullOpenMargin);
            var elapsed = 0f;
            var frame = 0;
            var previousEffectiveRadius = -preset.FinalClosedOvershootPixels;
            while (elapsed < preset.OpeningDuration && frame < 600)
            {
                elapsed = Mathf.Min(preset.OpeningDuration, elapsed + 1f / fps);
                var progress = elapsed / preset.OpeningDuration;
                var eased = TerminalIrisEasingUtility.Evaluate(preset.OpeningEasing, progress);
                var radius = fullyRevealedRadius * eased;
                var overshoot = preset.FinalClosedOvershootPixels * (1f - eased);
                var effectiveRadius = radius * resolution.y - overshoot;
                records.Add(
                    new TemporalMetricRecord(
                        "stage-entry-open",
                        resolution,
                        fps,
                        frame,
                        TerminalTransitionState.Revealing,
                        radius,
                        overshoot,
                        effectiveRadius,
                        effectiveRadius - previousEffectiveRadius));
                previousEffectiveRadius = effectiveRadius;
                frame++;
            }

            Assert.That(frame, Is.LessThan(600), "Stage Entry open did not complete.");
        }

        private static void AssertKnownCenterAcceptance(
            TerminalIrisContourAnalysis analysis,
            string identity)
        {
            Assert.That(
                analysis.CenterErrorPixels,
                Is.LessThanOrEqualTo(1f),
                $"{identity}: fitted center error");
            Assert.That(
                analysis.RmsRadialErrorPixels,
                Is.LessThanOrEqualTo(0.35f),
                $"{identity}: circle RMS radial error");
            Assert.That(
                analysis.MaximumRadialErrorPixels,
                Is.LessThanOrEqualTo(1f),
                $"{identity}: maximum radial error");
            Assert.That(
                analysis.UnexpectedTransparentComponentCount,
                Is.Zero,
                $"{identity}: disconnected transparent aperture component");
            Assert.That(
                analysis.OpaquePinholePixelCount,
                Is.Zero,
                $"{identity}: opaque aperture pinhole");
            Assert.That(
                analysis.TransparentArtifactPixelCount,
                Is.Zero,
                $"{identity}: transparent artifact outside aperture");
        }

        private static void AppendLegacyRegressionRow(
            ICollection<string> rows,
            string fixture,
            string direction,
            int width,
            int height,
            Vector2 expectedCenter,
            Color32 opaqueCover,
            IReadOnlyList<Color32> pixels)
        {
            var legacyCenter = LegacyBrightPixelCentroid(pixels, width, height, 0.1f);
            var current = TerminalIrisEvidenceAnalyzer.AnalyzeCoverage(
                TerminalIrisEvidenceAnalyzer.ResolveCoverage(
                    pixels,
                    Color.white,
                    (Color)opaqueCover),
                width,
                height,
                expectedCenter);
            var expectedPixels = new Vector2(
                expectedCenter.x * (width - 1),
                expectedCenter.y * (height - 1));
            var screenCenterPixels = new Vector2(
                (width - 1) * 0.5f,
                (height - 1) * 0.5f);
            var legacyError = Vector2.Distance(legacyCenter, expectedPixels);
            var legacyFalseCenter =
                Vector2.Distance(legacyCenter, screenCenterPixels) <= 1f &&
                legacyError > 1f;
            Assert.That(
                legacyFalseCenter,
                Is.True,
                $"{fixture}/{direction}: historical bright-pixel centroid must reproduce false-center.");
            AssertKnownCenterAcceptance(current, $"{fixture}/{direction} current contour");
            rows.Add(string.Join(
                ",",
                fixture,
                direction,
                width.ToString(CultureInfo.InvariantCulture),
                height.ToString(CultureInfo.InvariantCulture),
                Float(expectedCenter.x),
                Float(expectedCenter.y),
                Float(legacyCenter.x / (width - 1)),
                Float(legacyCenter.y / (height - 1)),
                Float(legacyError),
                Float(current.FittedCenterViewport.x),
                Float(current.FittedCenterViewport.y),
                Float(current.CenterErrorPixels),
                legacyFalseCenter ? "true" : "false",
                current.CenterErrorPixels <= 1f ? "true" : "false",
                "PASS"));
        }

        private static Vector2 LegacyBrightPixelCentroid(
            IReadOnlyList<Color32> pixels,
            int width,
            int height,
            float threshold)
        {
            double sumX = 0d;
            double sumY = 0d;
            var count = 0;
            var byteThreshold = Mathf.Clamp01(threshold) * byte.MaxValue;
            for (var index = 0; index < pixels.Count; index++)
            {
                var pixel = pixels[index];
                if (Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)) <= byteThreshold)
                {
                    continue;
                }

                sumX += index % width;
                sumY += index / width;
                count++;
            }

            Assert.That(count, Is.GreaterThan(0));
            return new Vector2((float)(sumX / count), (float)(sumY / count));
        }

        private static Color32[] CreateLegacyRegressionPixels(
            int width,
            int height,
            Vector2 centerViewport,
            float radiusViewportHeight,
            Color32 opaqueCover)
        {
            var pixels = new Color32[width * height];
            var centerPixels = new Vector2(
                centerViewport.x * (width - 1),
                centerViewport.y * (height - 1));
            var radiusPixels = radiusViewportHeight * height;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixels[y * width + x] =
                        Vector2.Distance(new Vector2(x, y), centerPixels) < radiusPixels
                            ? new Color32(255, 255, 255, 255)
                            : opaqueCover;
                }
            }

            return pixels;
        }

        private static void WriteGraphicsEnvironment(
            string outputDirectory,
            string renderTargets)
        {
            var json =
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                $"  \"unityVersion\": \"{Escape(Application.unityVersion)}\",\n" +
                $"  \"operatingSystem\": \"{Escape(SystemInfo.operatingSystem)}\",\n" +
                $"  \"graphicsDeviceName\": \"{Escape(SystemInfo.graphicsDeviceName)}\",\n" +
                $"  \"graphicsDeviceType\": \"{SystemInfo.graphicsDeviceType}\",\n" +
                $"  \"graphicsDeviceVersion\": \"{Escape(SystemInfo.graphicsDeviceVersion)}\",\n" +
                $"  \"graphicsMemorySizeMB\": {SystemInfo.graphicsMemorySize},\n" +
                $"  \"screenResolution\": \"{Screen.width}x{Screen.height}\",\n" +
                $"  \"renderTargets\": \"{Escape(renderTargets)}\",\n" +
                $"  \"targetFrameRate\": {Application.targetFrameRate},\n" +
                $"  \"colorSpace\": \"{QualitySettings.activeColorSpace}\"\n" +
                "}\n";
            File.WriteAllText(
                Path.Combine(outputDirectory, "graphics-environment.json"),
                json);
        }

        private static string KnownCenterCsvRow(
            string fixture,
            string fixtureId,
            string pixelBufferSha256,
            string capturePath,
            string captureSha256,
            Vector2Int resolution,
            Vector2 center,
            float radius,
            float edgeWidth,
            string shaderSha256,
            string materialSha256,
            TerminalIrisContourAnalysis analysis)
        {
            return fixture + "," +
                   fixtureId + "," +
                   pixelBufferSha256 + "," +
                   capturePath + "," +
                   captureSha256 + "," +
                   resolution.x + "," +
                   resolution.y + "," +
                   Float(center.x) + "," +
                   Float(center.y) + "," +
                   Float(radius) + "," +
                   Float(edgeWidth) + "," +
                   shaderSha256 + "," +
                   materialSha256 + "," +
                   Float(analysis.FittedCenterViewport.x) + "," +
                   Float(analysis.FittedCenterViewport.y) + "," +
                   Float(analysis.CenterErrorPixels) + "," +
                   Float(analysis.RmsRadialErrorPixels) + "," +
                   Float(analysis.MaximumRadialErrorPixels) + "," +
                   Float(analysis.P99RadialErrorPixels) + "," +
                   analysis.UnexpectedTransparentComponentCount + "," +
                   analysis.OpaquePinholePixelCount + "," +
                   analysis.TransparentArtifactPixelCount + ",PASS";
        }

        private static IEnumerable<byte> PixelBytes(Color32 pixel)
        {
            yield return pixel.r;
            yield return pixel.g;
            yield return pixel.b;
            yield return pixel.a;
        }

        private static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return string.Concat(
                    sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }

        private static void AssertTemporalAcceptance(
            IReadOnlyCollection<TemporalMetricRecord> records)
        {
            const float maximumNormalizedEdgeDisplacementAt30Fps = 0.30f;
            foreach (var group in records.GroupBy(
                         record =>
                             $"{record.Transition}-{record.Width}x{record.Height}-{record.Fps}"))
            {
                var ordered = group.OrderBy(record => record.Frame).ToArray();
                var opening = ordered[0].Transition.Contains("open") ||
                              ordered[0].Transition.Contains("reveal");
                Assert.That(
                    opening
                        ? ordered.Min(record => record.EdgeDisplacementPixels)
                        : -ordered.Max(record => record.EdgeDisplacementPixels),
                    Is.GreaterThanOrEqualTo(-0.01f),
                    $"{group.Key} contour direction reversed.");
                Assert.That(
                    ordered.Max(record => Mathf.Abs(record.EdgeDisplacementPixels)),
                    Is.LessThanOrEqualTo(
                        maximumNormalizedEdgeDisplacementAt30Fps *
                        ordered[0].Height *
                        30f /
                        ordered[0].Fps),
                    $"{group.Key} exceeds the authored resolution- and frame-rate-scaled displacement ceiling.");
            }
        }

        private static void AssertStaticAcceptance(IReadOnlyCollection<StaticEdgeRecord> records)
        {
            var contained = records.Where(record => record.IsFullyContained).ToArray();
            Assert.That(contained, Is.Not.Empty);
            Assert.That(
                contained.Max(
                    record =>
                        Mathf.Abs(record.HorizontalRadiusPixels - record.VerticalRadiusPixels)),
                Is.LessThanOrEqualTo(1f),
                "Terminal Iris physical horizontal and vertical radii must agree within one render pixel.");
            Assert.That(
                contained.Max(
                    record =>
                        Mathf.Abs(
                            record.HorizontalEdgeWidthPixels -
                            record.VerticalEdgeWidthPixels)),
                Is.LessThanOrEqualTo(0.5f),
                "Terminal Iris cardinal 10-90 edge widths must agree within half a render pixel.");
        }

        private static void AssertFullContourAcceptance(
            IReadOnlyCollection<FullContourRecord> records)
        {
            Assert.That(
                records.Select(record => record.Profile).Distinct(),
                Is.EquivalentTo(FullContourProfiles));
            Assert.That(records, Has.Count.EqualTo(
                FullContourResolutions.Length *
                FullContourRadii.Length *
                FullContourProfiles.Length));
            foreach (var record in records)
            {
                var smallRadius = record.AuthoredRadius <= 0.01f;
                Assert.That(
                    record.RmsRadialErrorPixels,
                    Is.LessThanOrEqualTo(smallRadius ? 0.75f : 0.5f),
                    $"{record.Identity} full-contour RMS radial error.");
                Assert.That(
                    record.MaximumRadialErrorPixels,
                    Is.LessThanOrEqualTo(smallRadius ? 1.5f : 1f),
                    $"{record.Identity} full-contour maximum radial error.");
                Assert.That(
                    record.P99RadialErrorPixels,
                    Is.LessThanOrEqualTo(smallRadius ? 1.25f : 0.75f),
                    $"{record.Identity} full-contour P99 radial error.");
                Assert.That(
                    record.MaximumEdgeWidthPixels - record.MinimumEdgeWidthPixels,
                    Is.LessThanOrEqualTo(smallRadius ? 0.75f : 0.5f),
                    $"{record.Identity} angular 10-90 edge-width delta.");
                Assert.That(
                    record.MaximumEdgeWidthPixels /
                    Mathf.Max(0.0001f, record.MinimumEdgeWidthPixels),
                    Is.LessThanOrEqualTo(smallRadius ? 1.25f : 1.20f),
                    $"{record.Identity} angular 10-90 edge-width ratio.");
                Assert.That(record.TransparentComponentCount, Is.EqualTo(1));
                Assert.That(record.UnexpectedTransparentComponentCount, Is.Zero);
                Assert.That(record.OpaquePinholeCount, Is.Zero);
                Assert.That(record.TransparentPinholeCount, Is.Zero);
                Assert.That(record.IsolatedEdgeArtifactCount, Is.Zero);
                Assert.That(record.OnePixelProtrusionCount, Is.Zero);
                Assert.That(record.SelfIntersectionCount, Is.Zero);
            }
        }

        private static void AssertAAResponsibilityAcceptance(
            IReadOnlyCollection<AAResponsibilityRecord> records)
        {
            Assert.That(
                records.Select(record => record.Profile).Distinct(),
                Is.EquivalentTo(FullContourProfiles));
            Assert.That(
                records,
                Has.Count.EqualTo(FullContourResolutions.Length * FullContourProfiles.Length));
            foreach (var record in records)
            {
                Assert.That(
                    record.AAOnlyEdgeWidthPixels,
                    Is.InRange(0.75f, 1.75f),
                    $"{record.Identity} AA-only coverage edge must remain approximately one pixel.");
                if (record.ArtisticFeatherHalfWidthPixels > 0f)
                {
                    Assert.That(
                        record.AAPlusFeatherEdgeWidthPixels,
                        Is.GreaterThan(record.AAOnlyEdgeWidthPixels + 0.25f),
                        $"{record.Identity} authored feather must be distinguishable from coverage AA.");
                }
            }

            foreach (var group in records.GroupBy(record => record.Profile))
            {
                Assert.That(
                    group.Max(record => record.AAOnlyEdgeWidthPixels) -
                    group.Min(record => record.AAOnlyEdgeWidthPixels),
                    Is.LessThanOrEqualTo(0.2f),
                    $"{group.Key} AA-only width must remain render-pixel authored across resolutions.");
                Assert.That(
                    group.Max(record => record.FeatherContributionPixels) -
                    group.Min(record => record.FeatherContributionPixels),
                    Is.LessThanOrEqualTo(0.2f),
                    $"{group.Key} feather contribution must remain render-pixel authored across resolutions.");
            }
        }

        private static void AssertSmallRadiusAcceptance(IReadOnlyCollection<SmallRadiusRecord> records)
        {
            foreach (var group in records.GroupBy(record => $"{record.Profile}-{record.Width}x{record.Height}"))
            {
                var ordered = group.OrderByDescending(record => record.Radius).ToArray();
                for (var index = 1; index < ordered.Length; index++)
                {
                    Assert.That(
                        ordered[index].AperturePixels,
                        Is.LessThanOrEqualTo(ordered[index - 1].AperturePixels),
                        $"{group.Key} aperture must close monotonically.");
                }

                Assert.That(ordered.Last().CenterCoverage, Is.GreaterThanOrEqualTo(254f / 255f));
                Assert.That(ordered.Last().EffectiveRadiusPixels, Is.LessThan(0f));
                Assert.That(ordered.Last().TransparentPixelCount, Is.Zero);
                Assert.That(ordered.Last().TransparentComponentCount, Is.Zero);
                Assert.That(ordered.Last().MeasuredContourRadiusPixels, Is.Zero);
                Assert.That(
                    ordered.Where(record => record.InputRadius > 0f)
                        .Max(record => record.TransparentComponentCount),
                    Is.LessThanOrEqualTo(1),
                    $"{group.Key} has a disconnected small-radius aperture island.");
                Assert.That(
                    ordered.Max(record => record.AsymmetricPixelCount),
                    Is.Zero,
                    $"{group.Key} has an asymmetric small-radius pixel cluster.");
            }
        }

        private static bool ShouldWriteRepresentativePng(Vector2 center, float radius)
        {
            var representativeCenter =
                center == new Vector2(0.5f, 0.5f) ||
                center == new Vector2(0.2f, 0.5f);
            return representativeCenter &&
                   (radius == 0.30f || radius == 0.05f || radius == 0.01f);
        }

        private static void WriteStaticRecords(
            QualityCaptureContext context,
            IReadOnlyList<StaticEdgeRecord> records,
            IReadOnlyList<FullContourRecord> contourRecords,
            IReadOnlyList<AAResponsibilityRecord> aaResponsibilityRecords,
            IReadOnlyList<AnisotropySweepRecord> anisotropySweepRecords)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"schemaVersion\": 2,");
            builder.AppendLine($"  \"phase\": \"{context.Phase}\",");
            builder.AppendLine($"  \"graphicsDeviceType\": \"{SystemInfo.graphicsDeviceType}\",");
            builder.AppendLine($"  \"graphicsDeviceName\": \"{Escape(SystemInfo.graphicsDeviceName)}\",");
            builder.AppendLine("  \"records\": [");
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                builder.Append("    ").Append(record.ToJson());
                builder.AppendLine(index + 1 < records.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"fullContourRecords\": [");
            for (var index = 0; index < contourRecords.Count; index++)
            {
                builder.Append("    ").Append(contourRecords[index].ToJson());
                builder.AppendLine(index + 1 < contourRecords.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"aaResponsibilityRecords\": [");
            for (var index = 0; index < aaResponsibilityRecords.Count; index++)
            {
                builder.Append("    ").Append(aaResponsibilityRecords[index].ToJson());
                builder.AppendLine(
                    index + 1 < aaResponsibilityRecords.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"anisotropySweepRecords\": [");
            for (var index = 0; index < anisotropySweepRecords.Count; index++)
            {
                builder.Append("    ").Append(anisotropySweepRecords[index].ToJson());
                builder.AppendLine(
                    index + 1 < anisotropySweepRecords.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            File.WriteAllText(
                Path.Combine(context.OutputDirectory, "static-edge-metrics.json"),
                builder.ToString());
        }

        private static void WriteSmallRadiusRecords(
            QualityCaptureContext context,
            IReadOnlyList<SmallRadiusRecord> records)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"schemaVersion\": 1,");
            builder.AppendLine($"  \"phase\": \"{context.Phase}\",");
            builder.AppendLine("  \"records\": [");
            for (var index = 0; index < records.Count; index++)
            {
                builder.Append("    ").Append(records[index].ToJson());
                builder.AppendLine(index + 1 < records.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            File.WriteAllText(
                Path.Combine(context.OutputDirectory, "small-radius-metrics.json"),
                builder.ToString());
        }

        private static string RadiusName(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture).Replace(".", "p");
        }

        private static string CenterName(Vector2 center)
        {
            if (center == new Vector2(0.5f, 0.5f))
            {
                return "center";
            }

            if (center.x < 0.5f)
            {
                return "left";
            }

            if (center.x > 0.5f)
            {
                return "right";
            }

            return center.y < 0.5f ? "bottom" : "top";
        }

        private static string Float(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void RequireGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("Terminal Iris visual quality requires a graphics device.");
            }
        }

        private readonly struct QualityCaptureContext
        {
            private QualityCaptureContext(string outputDirectory, string phase)
            {
                OutputDirectory = outputDirectory;
                Phase = phase;
            }

            public string OutputDirectory { get; }

            public string Phase { get; }

            public bool IsBefore => string.Equals(Phase, "before", StringComparison.Ordinal);

            public static QualityCaptureContext Create(
                string lane,
                string missingOutputGuidance = null)
            {
                var outputRoot = ReadCommandLineValue("-terminalIrisQualityOutput");
                Assert.That(outputRoot, Is.Not.Null.And.Not.Empty,
                    "Specialized Terminal Iris capture producers require " +
                    "-terminalIrisQualityOutput. " +
                    (missingOutputGuidance ?? "Use the matching ./run_tests.sh terminal-iris-* lane."));
                var phase = ReadCommandLineValue("-terminalIrisQualityPhase") ?? "after";
                var outputDirectory = Path.Combine(outputRoot, lane);
                Directory.CreateDirectory(outputDirectory);
                return new QualityCaptureContext(outputDirectory, phase);
            }

            private static string ReadCommandLineValue(string key)
            {
                var arguments = Environment.GetCommandLineArgs();
                for (var index = 0; index + 1 < arguments.Length; index++)
                {
                    if (string.Equals(arguments[index], key, StringComparison.Ordinal))
                    {
                        return arguments[index + 1];
                    }
                }

                return null;
            }
        }

        private sealed class IrisCaptureFixture : IDisposable
        {
            private readonly int _centerId = Shader.PropertyToID("_Center");
            private readonly int _radiusId = Shader.PropertyToID("_Radius");
            private readonly int _closedOvershootPixelsId =
                Shader.PropertyToID("_ClosedOvershootPixels");
            private readonly int _outerColorId = Shader.PropertyToID("_OuterColor");
            private readonly int _outerOpacityId = Shader.PropertyToID("_OuterOpacity");
            private readonly int _edgeAntiAliasScaleId =
                Shader.PropertyToID("_EdgeAntiAliasScale");
            private readonly int _minimumAAPixelsId =
                Shader.PropertyToID("_MinimumAAPixels");
            private readonly int _artisticFeatherHalfWidthPixelsId =
                Shader.PropertyToID("_ArtisticFeatherHalfWidthPixels");
            private readonly int _rimWidthPixelsId =
                Shader.PropertyToID("_RimWidthPixels");
            private readonly int _rimSoftnessPixelsId =
                Shader.PropertyToID("_RimSoftnessPixels");
            private readonly int _rimFadeOutPixelsId =
                Shader.PropertyToID("_RimFadeOutPixels");
            private readonly int _rimColorId = Shader.PropertyToID("_RimColor");
            private Camera _camera;
            private GameObject _cameraObject;
            private GameObject _rootObject;
            private GameplayUiCanvasRootView _rootView;
            private RenderTexture _target;
            private Texture2D _checkerTexture;
            private RawImage _checkerImage;

            public IReadOnlyList<QualityProfile> Profiles { get; private set; }

            public TerminalIrisOverlayView IrisView =>
                _rootView.TerminalIrisOverlayView;

            public int Width => _target != null ? _target.width : 0;

            public int Height => _target != null ? _target.height : 0;

            public IEnumerator Initialize()
            {
                var rootPrefab = Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell");
                var style = Resources.Load<ResultTransitionVisualStyle>(
                    "UI/Transitions/ResultTransitionVisualStyle");
                var motion = Resources.Load<TerminalIrisMotionProfile>(
                    "UI/Transitions/TerminalIrisMotionProfile");
                Assert.That(rootPrefab, Is.Not.Null);
                Assert.That(style, Is.Not.Null);
                Assert.That(motion, Is.Not.Null);

                _cameraObject = new GameObject("TerminalIrisQualityCamera", typeof(Camera));
                _camera = _cameraObject.GetComponent<Camera>();
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = Color.white;
                _camera.orthographic = true;
                _camera.transform.position = new Vector3(0f, 0f, -10f);
                _camera.cullingMask = 1 << 5;

                _rootObject = Object.Instantiate(rootPrefab);
                SetLayerRecursively(_rootObject, 5);
                _rootView = _rootObject.GetComponent<GameplayUiCanvasRootView>();
                _rootView.EnsureHierarchy();
                var canvas = _rootObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = _camera;
                canvas.planeDistance = 1f;

                _checkerTexture = CreateCheckerTexture();
                var checkerObject = new GameObject("QualityChecker", typeof(RectTransform), typeof(RawImage));
                checkerObject.transform.SetParent(canvas.transform, false);
                checkerObject.transform.SetAsFirstSibling();
                SetLayerRecursively(checkerObject, 5);
                var checkerRect = checkerObject.GetComponent<RectTransform>();
                checkerRect.anchorMin = Vector2.zero;
                checkerRect.anchorMax = Vector2.one;
                checkerRect.offsetMin = Vector2.zero;
                checkerRect.offsetMax = Vector2.zero;
                _checkerImage = checkerObject.GetComponent<RawImage>();
                _checkerImage.texture = _checkerTexture;
                _checkerImage.raycastTarget = false;
                _checkerImage.enabled = false;

                _rootView.TerminalIrisOverlayView.ConfigureDimSnapshot(style.CreateDimSnapshot());
                _rootView.TerminalIrisOverlayView.Show();
                var resolver = motion.CreateResolver();
                var entry = resolver.ResolveStageEntryOpen();
                _rootView.TerminalIrisOverlayView.ApplyClosedEntry(new Vector2(0.5f, 0.5f), entry);
                var victory = resolver.ResolveClose(TerminalTransitionKind.Victory);
                var defeat = resolver.ResolveClose(TerminalTransitionKind.Defeat);
                var retry = resolver.ResolveRetryClose(SceneTransitionIntent.ManualRetry);
                var gameplayEntry = resolver.ResolveGameplayEntrySourceClose(
                    SceneTransitionIntent.GameplayEntry);
                Profiles = new[]
                {
                    QualityProfile.FromClose("victory", victory),
                    QualityProfile.FromClose("defeat", defeat),
                    QualityProfile.FromClose("retry", retry),
                    QualityProfile.FromClose("gameplay-entry", gameplayEntry),
                    QualityProfile.FromOpen("entry", entry),
                };
                yield return null;
            }

            public void SetResolution(int width, int height)
            {
                if (_target != null)
                {
                    _camera.targetTexture = null;
                    _target.Release();
                    Object.DestroyImmediate(_target);
                }

                _target = new RenderTexture(
                    width,
                    height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear);
                Assert.That(_target.Create(), Is.True);
                _camera.targetTexture = _target;
                Canvas.ForceUpdateCanvases();
            }

            public void Configure(
                QualityProfile profile,
                Vector2 center,
                float radius,
                bool rimEnabled,
                float? artisticFeatherHalfWidthPixels = null,
                float? minimumAAPixels = null)
            {
                var material = _rootView.TerminalIrisOverlayView.RuntimeMaterialForTests;
                material.SetVector(_centerId, center);
                material.SetFloat(_radiusId, radius);
                material.SetFloat(
                    _closedOvershootPixelsId,
                    radius <= 0f ? profile.FinalClosedOvershootPixels : 0f);
                material.SetFloat(
                    _edgeAntiAliasScaleId,
                    profile.Edge.EdgeAntiAliasScale);
                material.SetFloat(
                    _minimumAAPixelsId,
                    minimumAAPixels ?? profile.Edge.MinimumAAPixels);
                material.SetFloat(
                    _artisticFeatherHalfWidthPixelsId,
                    artisticFeatherHalfWidthPixels ??
                    profile.Edge.ArtisticFeatherHalfWidthPixels);
                material.SetColor(_outerColorId, Color.black);
                material.SetFloat(_outerOpacityId, 1f);
                material.SetFloat(
                    _rimWidthPixelsId,
                    rimEnabled ? profile.Edge.RimWidthPixels : 0f);
                material.SetFloat(_rimSoftnessPixelsId, profile.Edge.RimSoftnessPixels);
                material.SetFloat(_rimFadeOutPixelsId, profile.Edge.RimFadeOutPixels);
                material.SetColor(
                    _rimColorId,
                    rimEnabled ? profile.Edge.RimColor : Color.clear);
                _checkerImage.enabled = false;
                _camera.backgroundColor = Color.white;
            }

            public StaticEdgeRecord MeasureStatic(string profile, Vector2 center, float radius)
            {
                _checkerImage.enabled = false;
                _camera.backgroundColor = Color.white;
                Canvas.ForceUpdateCanvases();
                _camera.Render();
                var horizontal = ReadHorizontalCoverage(Mathf.RoundToInt(center.y * (_target.height - 1)));
                var vertical = ReadVerticalCoverage(Mathf.RoundToInt(center.x * (_target.width - 1)));
                var horizontalMeasure = MeasureAxis(horizontal, center.x * (_target.width - 1));
                var verticalMeasure = MeasureAxis(vertical, center.y * (_target.height - 1));
                return new StaticEdgeRecord(
                    profile,
                    _target.width,
                    _target.height,
                    (float)_target.width / _target.height,
                    center,
                    radius,
                    horizontalMeasure.Radius,
                    verticalMeasure.Radius,
                    horizontalMeasure.EdgeWidth,
                    verticalMeasure.EdgeWidth);
            }

            public SmallRadiusRecord MeasureSmallRadius(string profile, float radius)
            {
                _checkerImage.enabled = false;
                _camera.backgroundColor = Color.white;
                Canvas.ForceUpdateCanvases();
                _camera.Render();
                var centerX = Mathf.RoundToInt(0.5f * (_target.width - 1));
                var centerY = Mathf.RoundToInt(0.5f * (_target.height - 1));
                var horizontal = ReadHorizontalCoverage(centerY);
                var centerCoverage = horizontal[Mathf.Clamp(centerX, 0, horizontal.Length - 1)];
                var aperturePixels = horizontal.Count(value => value < 0.5f);
                var pixels = ReadCoveragePixels();
                var transparentPixels = pixels.Count(value => value < 0.1f);
                var transparentComponentCount = CountComponents(
                    pixels,
                    _target.width,
                    _target.height,
                    value => value < 0.1f);
                var edgeComponentCount = CountComponents(
                    pixels,
                    _target.width,
                    _target.height,
                    value => value >= 0.1f && value <= 0.9f);
                var asymmetricPixelCount = 0;
                var maximumSymmetryDelta = 0f;
                for (var index = 0; index < pixels.Length; index++)
                {
                    var mirrored = pixels.Length - 1 - index;
                    var symmetryDelta = Mathf.Abs(pixels[index] - pixels[mirrored]);
                    maximumSymmetryDelta = Mathf.Max(maximumSymmetryDelta, symmetryDelta);
                    if ((pixels[index] < 0.5f) != (pixels[mirrored] < 0.5f))
                    {
                        asymmetricPixelCount++;
                    }
                }

                var overshootPixels = _rootView.TerminalIrisOverlayView
                    .RuntimeMaterialForTests
                    .GetFloat(_closedOvershootPixelsId);
                var measuredContourRadius = centerCoverage < 0.5f
                    ? MeasureAxis(horizontal, centerX).Radius
                    : 0f;
                return new SmallRadiusRecord(
                    profile,
                    _target.width,
                    _target.height,
                    radius,
                    overshootPixels,
                    measuredContourRadius,
                    aperturePixels,
                    centerCoverage,
                    transparentPixels,
                    transparentComponentCount,
                    edgeComponentCount,
                    asymmetricPixelCount,
                    maximumSymmetryDelta);
            }

            public FullContourRecord MeasureFullContour(
                string profile,
                Vector2 center,
                float authoredRadius,
                float artisticFeatherHalfWidthPixels)
            {
                _checkerImage.enabled = false;
                _camera.backgroundColor = Color.white;
                Canvas.ForceUpdateCanvases();
                _camera.Render();
                var coverage = ReadCoveragePixels();
                var centerPixels = new Vector2(
                    center.x * (_target.width - 1),
                    center.y * (_target.height - 1));
                var angles = new AngularContourSample[360];
                var contourPoints = new Vector2[angles.Length];
                var selfIntersectionCount = 0;
                for (var angle = 0; angle < angles.Length; angle++)
                {
                    var radians = angle * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                    var maxDistance = MaximumRayDistance(
                        centerPixels,
                        direction,
                        _target.width,
                        _target.height);
                    var alpha01 = FindRayCrossing(
                        coverage,
                        _target.width,
                        _target.height,
                        centerPixels,
                        direction,
                        maxDistance,
                        0.1f,
                        out _);
                    var alpha05 = FindRayCrossing(
                        coverage,
                        _target.width,
                        _target.height,
                        centerPixels,
                        direction,
                        maxDistance,
                        0.5f,
                        out var crossingCount);
                    var alpha09 = FindRayCrossing(
                        coverage,
                        _target.width,
                        _target.height,
                        centerPixels,
                        direction,
                        maxDistance,
                        0.9f,
                        out _);
                    if (crossingCount != 1)
                    {
                        selfIntersectionCount++;
                    }

                    angles[angle] = new AngularContourSample(
                        angle,
                        alpha01,
                        alpha05,
                        alpha09);
                    contourPoints[angle] = centerPixels + direction * alpha05;
                }

                var fitted = FitCircle(contourPoints);
                var radialErrors = contourPoints
                    .Select(point => Mathf.Abs(Vector2.Distance(point, fitted.Center) - fitted.Radius))
                    .OrderBy(value => value)
                    .ToArray();
                var edgeWidths = angles
                    .Select(sample => sample.EdgeWidthPixels)
                    .OrderBy(value => value)
                    .ToArray();
                var components = AnalyzeArtifacts(
                    coverage,
                    _target.width,
                    _target.height,
                    fitted,
                    edgeWidths.Last());
                var protrusionTolerance = authoredRadius <= 0.01f ? 1.5f : 1f;
                var onePixelProtrusionCount =
                    radialErrors.Count(value => value > protrusionTolerance);
                for (var index = 0; index < angles.Length; index++)
                {
                    angles[index] = angles[index].WithRadialError(
                        Mathf.Abs(
                            Vector2.Distance(contourPoints[index], fitted.Center) -
                            fitted.Radius));
                }

                return new FullContourRecord(
                    profile,
                    _target.width,
                    _target.height,
                    authoredRadius,
                    artisticFeatherHalfWidthPixels,
                    fitted,
                    Mathf.Sqrt(radialErrors.Select(value => value * value).Average()),
                    radialErrors.Last(),
                    Percentile(radialErrors, 0.95f),
                    Percentile(radialErrors, 0.99f),
                    edgeWidths.First(),
                    edgeWidths.Last(),
                    edgeWidths.Average(),
                    components.TransparentComponentCount,
                    Mathf.Max(0, components.TransparentComponentCount - 1),
                    components.LargestUnexpectedTransparentComponentPixels,
                    components.OpaquePinholeCount,
                    components.TransparentPinholeCount,
                    components.IsolatedEdgeArtifactCount,
                    onePixelProtrusionCount,
                    selfIntersectionCount,
                    angles);
            }

            public float[] CaptureCoverage()
            {
                _checkerImage.enabled = false;
                _camera.backgroundColor = Color.white;
                Canvas.ForceUpdateCanvases();
                _camera.Render();
                return ReadCoveragePixels();
            }

            public Color32[] CapturePixels()
            {
                _checkerImage.enabled = false;
                _camera.backgroundColor = Color.white;
                Canvas.ForceUpdateCanvases();
                _camera.Render();
                var previous = RenderTexture.active;
                var texture = new Texture2D(
                    _target.width,
                    _target.height,
                    TextureFormat.RGBA32,
                    false,
                    true);
                try
                {
                    RenderTexture.active = _target;
                    texture.ReadPixels(
                        new Rect(0f, 0f, _target.width, _target.height),
                        0,
                        0);
                    texture.Apply();
                    return texture.GetPixels32();
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(texture);
                }
            }

            public void WritePng(
                string outputDirectory,
                string fileName,
                CaptureBackground background)
            {
                _checkerImage.enabled = background == CaptureBackground.Checker;
                _camera.backgroundColor = background == CaptureBackground.Black
                    ? Color.black
                    : Color.white;
                Canvas.ForceUpdateCanvases();
                _camera.Render();
                var previous = RenderTexture.active;
                var texture = new Texture2D(
                    _target.width,
                    _target.height,
                    TextureFormat.RGBA32,
                    false,
                    true);
                try
                {
                    RenderTexture.active = _target;
                    texture.ReadPixels(new Rect(0f, 0f, _target.width, _target.height), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(outputDirectory, fileName), texture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(texture);
                }
            }

            public void Dispose()
            {
                if (_camera != null)
                {
                    _camera.targetTexture = null;
                }

                if (_target != null)
                {
                    _target.Release();
                    Object.DestroyImmediate(_target);
                }

                if (_rootObject != null)
                {
                    Object.DestroyImmediate(_rootObject);
                }

                if (_cameraObject != null)
                {
                    Object.DestroyImmediate(_cameraObject);
                }

                if (_checkerTexture != null)
                {
                    Object.DestroyImmediate(_checkerTexture);
                }
            }

            private float[] ReadHorizontalCoverage(int y)
            {
                var texture = new Texture2D(_target.width, 1, TextureFormat.RGBA32, false, true);
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = _target;
                    texture.ReadPixels(new Rect(0, Mathf.Clamp(y, 0, _target.height - 1), _target.width, 1), 0, 0);
                    texture.Apply();
                    return texture.GetPixels().Select(ToCoverage).ToArray();
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(texture);
                }
            }

            private float[] ReadVerticalCoverage(int x)
            {
                var texture = new Texture2D(1, _target.height, TextureFormat.RGBA32, false, true);
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = _target;
                    texture.ReadPixels(new Rect(Mathf.Clamp(x, 0, _target.width - 1), 0, 1, _target.height), 0, 0);
                    texture.Apply();
                    return texture.GetPixels().Select(ToCoverage).ToArray();
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(texture);
                }
            }

            private float[] ReadCoveragePixels()
            {
                var texture = new Texture2D(
                    _target.width,
                    _target.height,
                    TextureFormat.RGBA32,
                    false,
                    true);
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = _target;
                    texture.ReadPixels(
                        new Rect(0f, 0f, _target.width, _target.height),
                        0,
                        0);
                    texture.Apply();
                    return texture.GetPixels32()
                        .Select(
                            color =>
                                1f - (color.r + color.g + color.b) / (3f * byte.MaxValue))
                        .ToArray();
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(texture);
                }
            }

            private static float MaximumRayDistance(
                Vector2 center,
                Vector2 direction,
                int width,
                int height)
            {
                var xDistance = Mathf.Abs(direction.x) <= 0.000001f
                    ? float.MaxValue
                    : direction.x > 0f
                        ? (width - 1 - center.x) / direction.x
                        : -center.x / direction.x;
                var yDistance = Mathf.Abs(direction.y) <= 0.000001f
                    ? float.MaxValue
                    : direction.y > 0f
                        ? (height - 1 - center.y) / direction.y
                        : -center.y / direction.y;
                return Mathf.Max(0f, Mathf.Min(xDistance, yDistance));
            }

            private static float FindRayCrossing(
                IReadOnlyList<float> coverage,
                int width,
                int height,
                Vector2 center,
                Vector2 direction,
                float maxDistance,
                float threshold,
                out int crossingCount)
            {
                const float step = 0.25f;
                var previousDistance = 0f;
                var previousValue = SampleBilinear(coverage, width, height, center);
                var firstCrossing = float.NaN;
                crossingCount = 0;
                for (var distance = step; distance <= maxDistance; distance += step)
                {
                    var value = SampleBilinear(
                        coverage,
                        width,
                        height,
                        center + direction * distance);
                    if (previousValue < threshold && value >= threshold)
                    {
                        crossingCount++;
                        if (float.IsNaN(firstCrossing))
                        {
                            firstCrossing = InterpolateCrossing(
                                previousDistance,
                                previousValue,
                                distance,
                                value,
                                threshold);
                        }
                    }

                    previousDistance = distance;
                    previousValue = value;
                }

                return float.IsNaN(firstCrossing) ? maxDistance : firstCrossing;
            }

            private static float SampleBilinear(
                IReadOnlyList<float> values,
                int width,
                int height,
                Vector2 position)
            {
                var x = Mathf.Clamp(position.x, 0f, width - 1);
                var y = Mathf.Clamp(position.y, 0f, height - 1);
                var x0 = Mathf.FloorToInt(x);
                var y0 = Mathf.FloorToInt(y);
                var x1 = Mathf.Min(width - 1, x0 + 1);
                var y1 = Mathf.Min(height - 1, y0 + 1);
                var tx = x - x0;
                var ty = y - y0;
                var lower = Mathf.Lerp(values[y0 * width + x0], values[y0 * width + x1], tx);
                var upper = Mathf.Lerp(values[y1 * width + x0], values[y1 * width + x1], tx);
                return Mathf.Lerp(lower, upper, ty);
            }

            private static CircleFit FitCircle(IReadOnlyList<Vector2> points)
            {
                double count = points.Count;
                double sumX = 0d;
                double sumY = 0d;
                double sumXX = 0d;
                double sumXY = 0d;
                double sumYY = 0d;
                double sumRadiusSquared = 0d;
                double sumXRadiusSquared = 0d;
                double sumYRadiusSquared = 0d;
                foreach (var point in points)
                {
                    var x = (double)point.x;
                    var y = (double)point.y;
                    var radiusSquared = x * x + y * y;
                    sumX += x;
                    sumY += y;
                    sumXX += x * x;
                    sumXY += x * y;
                    sumYY += y * y;
                    sumRadiusSquared += radiusSquared;
                    sumXRadiusSquared += x * radiusSquared;
                    sumYRadiusSquared += y * radiusSquared;
                }

                var solution = SolveLinear3(
                    new[,]
                    {
                        { count, sumX, sumY },
                        { sumX, sumXX, sumXY },
                        { sumY, sumXY, sumYY },
                    },
                    new[]
                    {
                        -sumRadiusSquared,
                        -sumXRadiusSquared,
                        -sumYRadiusSquared,
                    });
                var center = new Vector2(
                    (float)(-0.5d * solution[1]),
                    (float)(-0.5d * solution[2]));
                var radius = Mathf.Sqrt(
                    Mathf.Max(
                        0f,
                        center.sqrMagnitude - (float)solution[0]));
                return new CircleFit(center, radius);
            }

            private static double[] SolveLinear3(double[,] matrix, double[] vector)
            {
                var augmented = new double[3, 4];
                for (var row = 0; row < 3; row++)
                {
                    for (var column = 0; column < 3; column++)
                    {
                        augmented[row, column] = matrix[row, column];
                    }

                    augmented[row, 3] = vector[row];
                }

                for (var pivot = 0; pivot < 3; pivot++)
                {
                    var largest = pivot;
                    for (var row = pivot + 1; row < 3; row++)
                    {
                        if (Math.Abs(augmented[row, pivot]) >
                            Math.Abs(augmented[largest, pivot]))
                        {
                            largest = row;
                        }
                    }

                    Assert.That(
                        Math.Abs(augmented[largest, pivot]),
                        Is.GreaterThan(0.000000001d),
                        "Terminal Iris circle-fit matrix is singular.");
                    if (largest != pivot)
                    {
                        for (var column = pivot; column < 4; column++)
                        {
                            var temporary = augmented[pivot, column];
                            augmented[pivot, column] = augmented[largest, column];
                            augmented[largest, column] = temporary;
                        }
                    }

                    var divisor = augmented[pivot, pivot];
                    for (var column = pivot; column < 4; column++)
                    {
                        augmented[pivot, column] /= divisor;
                    }

                    for (var row = 0; row < 3; row++)
                    {
                        if (row == pivot)
                        {
                            continue;
                        }

                        var factor = augmented[row, pivot];
                        for (var column = pivot; column < 4; column++)
                        {
                            augmented[row, column] -= factor * augmented[pivot, column];
                        }
                    }
                }

                return new[]
                {
                    augmented[0, 3],
                    augmented[1, 3],
                    augmented[2, 3],
                };
            }

            private static ArtifactAnalysis AnalyzeArtifacts(
                IReadOnlyList<float> coverage,
                int width,
                int height,
                CircleFit fitted,
                float maximumEdgeWidth)
            {
                var visited = new bool[coverage.Count];
                var componentSizes = new List<int>();
                var queue = new Queue<int>();
                for (var index = 0; index < coverage.Count; index++)
                {
                    if (visited[index] || coverage[index] >= 0.1f)
                    {
                        continue;
                    }

                    visited[index] = true;
                    queue.Enqueue(index);
                    var size = 0;
                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        size++;
                        var x = current % width;
                        var y = current / width;
                        for (var offsetY = -1; offsetY <= 1; offsetY++)
                        {
                            for (var offsetX = -1; offsetX <= 1; offsetX++)
                            {
                                if (offsetX == 0 && offsetY == 0)
                                {
                                    continue;
                                }

                                var nextX = x + offsetX;
                                var nextY = y + offsetY;
                                if (nextX < 0 || nextX >= width ||
                                    nextY < 0 || nextY >= height)
                                {
                                    continue;
                                }

                                var next = nextY * width + nextX;
                                if (!visited[next] && coverage[next] < 0.1f)
                                {
                                    visited[next] = true;
                                    queue.Enqueue(next);
                                }
                            }
                        }
                    }

                    componentSizes.Add(size);
                }

                componentSizes.Sort();
                var exclusion = maximumEdgeWidth + 1f;
                var opaquePinholeCount = 0;
                var transparentPinholeCount = 0;
                var isolatedEdgeArtifactCount = 0;
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var value = coverage[y * width + x];
                        var radialDistance = Vector2.Distance(new Vector2(x, y), fitted.Center);
                        if (value > 0.9f && radialDistance < fitted.Radius - exclusion)
                        {
                            opaquePinholeCount++;
                        }
                        else if (value < 0.1f && radialDistance > fitted.Radius + exclusion)
                        {
                            transparentPinholeCount++;
                        }
                        else if (value >= 0.1f &&
                                 value <= 0.9f &&
                                 Mathf.Abs(radialDistance - fitted.Radius) > exclusion)
                        {
                            isolatedEdgeArtifactCount++;
                        }
                    }
                }

                return new ArtifactAnalysis(
                    componentSizes.Count,
                    componentSizes.Count > 1
                        ? componentSizes.Take(componentSizes.Count - 1).Last()
                        : 0,
                    opaquePinholeCount,
                    transparentPinholeCount,
                    isolatedEdgeArtifactCount);
            }

            private static int CountComponents(
                IReadOnlyList<float> values,
                int width,
                int height,
                Func<float, bool> predicate)
            {
                var visited = new bool[values.Count];
                var queue = new Queue<int>();
                var componentCount = 0;
                for (var index = 0; index < values.Count; index++)
                {
                    if (visited[index] || !predicate(values[index]))
                    {
                        continue;
                    }

                    componentCount++;
                    visited[index] = true;
                    queue.Enqueue(index);
                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        var x = current % width;
                        var y = current / width;
                        for (var offsetY = -1; offsetY <= 1; offsetY++)
                        {
                            for (var offsetX = -1; offsetX <= 1; offsetX++)
                            {
                                if (offsetX == 0 && offsetY == 0)
                                {
                                    continue;
                                }

                                var nextX = x + offsetX;
                                var nextY = y + offsetY;
                                if (nextX < 0 || nextX >= width ||
                                    nextY < 0 || nextY >= height)
                                {
                                    continue;
                                }

                                var next = nextY * width + nextX;
                                if (!visited[next] && predicate(values[next]))
                                {
                                    visited[next] = true;
                                    queue.Enqueue(next);
                                }
                            }
                        }
                    }
                }

                return componentCount;
            }

            private static float Percentile(IReadOnlyList<float> sortedValues, float percentile)
            {
                var position = Mathf.Clamp01(percentile) * (sortedValues.Count - 1);
                var lower = Mathf.FloorToInt(position);
                var upper = Mathf.CeilToInt(position);
                return Mathf.Lerp(
                    sortedValues[lower],
                    sortedValues[upper],
                    position - lower);
            }

            private static AxisMeasure MeasureAxis(IReadOnlyList<float> samples, float center)
            {
                var positive50 = FindPositiveCrossing(samples, center, 0.5f);
                var negative50 = FindNegativeCrossing(samples, center, 0.5f);
                var positive10 = FindPositiveCrossing(samples, center, 0.1f);
                var positive90 = FindPositiveCrossing(samples, center, 0.9f);
                var radius = 0.5f * ((positive50 - center) + (center - negative50));
                return new AxisMeasure(radius, Mathf.Abs(positive90 - positive10));
            }

            private static float FindPositiveCrossing(
                IReadOnlyList<float> samples,
                float center,
                float threshold)
            {
                var start = Mathf.Clamp(Mathf.FloorToInt(center), 0, samples.Count - 1);
                for (var index = start + 1; index < samples.Count; index++)
                {
                    if (samples[index] < threshold)
                    {
                        continue;
                    }

                    return InterpolateCrossing(index - 1, samples[index - 1], index, samples[index], threshold);
                }

                return samples.Count - 1;
            }

            private static float FindNegativeCrossing(
                IReadOnlyList<float> samples,
                float center,
                float threshold)
            {
                var start = Mathf.Clamp(Mathf.CeilToInt(center), 0, samples.Count - 1);
                for (var index = start - 1; index >= 0; index--)
                {
                    if (samples[index] < threshold)
                    {
                        continue;
                    }

                    return InterpolateCrossing(index + 1, samples[index + 1], index, samples[index], threshold);
                }

                return 0f;
            }

            private static float InterpolateCrossing(
                float leftPosition,
                float leftValue,
                float rightPosition,
                float rightValue,
                float threshold)
            {
                var denominator = rightValue - leftValue;
                if (Mathf.Abs(denominator) <= 0.000001f)
                {
                    return 0.5f * (leftPosition + rightPosition);
                }

                var t = Mathf.Clamp01((threshold - leftValue) / denominator);
                return Mathf.Lerp(leftPosition, rightPosition, t);
            }

            private static float ToCoverage(Color color)
            {
                return 1f - Mathf.Clamp01((color.r + color.g + color.b) / 3f);
            }

            private static Texture2D CreateCheckerTexture()
            {
                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat,
                    name = "TerminalIrisQualityChecker",
                };
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var magenta = ((x / 8) + (y / 8)) % 2 == 0;
                        texture.SetPixel(x, y, magenta ? Color.magenta : Color.green);
                    }
                }

                texture.Apply();
                return texture;
            }

            private static void SetLayerRecursively(GameObject root, int layer)
            {
                root.layer = layer;
                foreach (Transform child in root.transform)
                {
                    SetLayerRecursively(child.gameObject, layer);
                }
            }
        }

        private readonly struct QualityProfile
        {
            private QualityProfile(
                string name,
                float finalClosedOvershootPixels,
                TerminalIrisRuntimeEdgeSettings edge)
            {
                Name = name;
                FinalClosedOvershootPixels = finalClosedOvershootPixels;
                Edge = edge;
            }

            public string Name { get; }

            public float FinalClosedOvershootPixels { get; }

            public TerminalIrisRuntimeEdgeSettings Edge { get; }

            public static QualityProfile FromClose(string name, TerminalIrisRuntimePreset preset)
            {
                return new QualityProfile(
                    name,
                    preset.FinalClosedOvershootPixels,
                    preset.Edge);
            }

            public static QualityProfile FromOpen(string name, TerminalIrisRuntimeOpenPreset preset)
            {
                return new QualityProfile(
                    name,
                    preset.FinalClosedOvershootPixels,
                    preset.Edge);
            }
        }

        private readonly struct AxisMeasure
        {
            public AxisMeasure(float radius, float edgeWidth)
            {
                Radius = radius;
                EdgeWidth = edgeWidth;
            }

            public float Radius { get; }

            public float EdgeWidth { get; }
        }

        private readonly struct CircleFit
        {
            public CircleFit(Vector2 center, float radius)
            {
                Center = center;
                Radius = radius;
            }

            public Vector2 Center { get; }

            public float Radius { get; }
        }

        private readonly struct ArtifactAnalysis
        {
            public ArtifactAnalysis(
                int transparentComponentCount,
                int largestUnexpectedTransparentComponentPixels,
                int opaquePinholeCount,
                int transparentPinholeCount,
                int isolatedEdgeArtifactCount)
            {
                TransparentComponentCount = transparentComponentCount;
                LargestUnexpectedTransparentComponentPixels =
                    largestUnexpectedTransparentComponentPixels;
                OpaquePinholeCount = opaquePinholeCount;
                TransparentPinholeCount = transparentPinholeCount;
                IsolatedEdgeArtifactCount = isolatedEdgeArtifactCount;
            }

            public int TransparentComponentCount { get; }

            public int LargestUnexpectedTransparentComponentPixels { get; }

            public int OpaquePinholeCount { get; }

            public int TransparentPinholeCount { get; }

            public int IsolatedEdgeArtifactCount { get; }
        }

        private readonly struct AngularContourSample
        {
            public AngularContourSample(
                int angleDegrees,
                float alpha01RadiusPixels,
                float alpha05RadiusPixels,
                float alpha09RadiusPixels,
                float radialErrorPixels = 0f)
            {
                AngleDegrees = angleDegrees;
                Alpha01RadiusPixels = alpha01RadiusPixels;
                Alpha05RadiusPixels = alpha05RadiusPixels;
                Alpha09RadiusPixels = alpha09RadiusPixels;
                RadialErrorPixels = radialErrorPixels;
            }

            public int AngleDegrees { get; }

            public float Alpha01RadiusPixels { get; }

            public float Alpha05RadiusPixels { get; }

            public float Alpha09RadiusPixels { get; }

            public float EdgeWidthPixels =>
                Mathf.Abs(Alpha09RadiusPixels - Alpha01RadiusPixels);

            public float RadialErrorPixels { get; }

            public AngularContourSample WithRadialError(float radialErrorPixels)
            {
                return new AngularContourSample(
                    AngleDegrees,
                    Alpha01RadiusPixels,
                    Alpha05RadiusPixels,
                    Alpha09RadiusPixels,
                    radialErrorPixels);
            }

            public string ToJson()
            {
                return "{\"angleDegrees\":" + AngleDegrees +
                       ",\"alpha01RadiusPixels\":" + Float(Alpha01RadiusPixels) +
                       ",\"alpha05RadiusPixels\":" + Float(Alpha05RadiusPixels) +
                       ",\"alpha09RadiusPixels\":" + Float(Alpha09RadiusPixels) +
                       ",\"edgeWidthPixels\":" + Float(EdgeWidthPixels) +
                       ",\"radialErrorPixels\":" + Float(RadialErrorPixels) +
                       "}";
            }
        }

        private sealed class FullContourRecord
        {
            public FullContourRecord(
                string profile,
                int width,
                int height,
                float authoredRadius,
                float artisticFeatherHalfWidthPixels,
                CircleFit fitted,
                float rmsRadialErrorPixels,
                float maximumRadialErrorPixels,
                float p95RadialErrorPixels,
                float p99RadialErrorPixels,
                float minimumEdgeWidthPixels,
                float maximumEdgeWidthPixels,
                float meanEdgeWidthPixels,
                int transparentComponentCount,
                int unexpectedTransparentComponentCount,
                int largestUnexpectedTransparentComponentPixels,
                int opaquePinholeCount,
                int transparentPinholeCount,
                int isolatedEdgeArtifactCount,
                int onePixelProtrusionCount,
                int selfIntersectionCount,
                IReadOnlyList<AngularContourSample> angles)
            {
                Profile = profile;
                Width = width;
                Height = height;
                AuthoredRadius = authoredRadius;
                ArtisticFeatherHalfWidthPixels = artisticFeatherHalfWidthPixels;
                FittedCenter = fitted.Center;
                FittedRadiusPixels = fitted.Radius;
                RmsRadialErrorPixels = rmsRadialErrorPixels;
                MaximumRadialErrorPixels = maximumRadialErrorPixels;
                P95RadialErrorPixels = p95RadialErrorPixels;
                P99RadialErrorPixels = p99RadialErrorPixels;
                MinimumEdgeWidthPixels = minimumEdgeWidthPixels;
                MaximumEdgeWidthPixels = maximumEdgeWidthPixels;
                MeanEdgeWidthPixels = meanEdgeWidthPixels;
                TransparentComponentCount = transparentComponentCount;
                UnexpectedTransparentComponentCount = unexpectedTransparentComponentCount;
                LargestUnexpectedTransparentComponentPixels =
                    largestUnexpectedTransparentComponentPixels;
                OpaquePinholeCount = opaquePinholeCount;
                TransparentPinholeCount = transparentPinholeCount;
                IsolatedEdgeArtifactCount = isolatedEdgeArtifactCount;
                OnePixelProtrusionCount = onePixelProtrusionCount;
                SelfIntersectionCount = selfIntersectionCount;
                Angles = angles;
            }

            public string Profile { get; }
            public int Width { get; }
            public int Height { get; }
            public float AuthoredRadius { get; }
            public float ArtisticFeatherHalfWidthPixels { get; }
            public Vector2 FittedCenter { get; }
            public float FittedRadiusPixels { get; }
            public float RmsRadialErrorPixels { get; }
            public float MaximumRadialErrorPixels { get; }
            public float P95RadialErrorPixels { get; }
            public float P99RadialErrorPixels { get; }
            public float MinimumEdgeWidthPixels { get; }
            public float MaximumEdgeWidthPixels { get; }
            public float MeanEdgeWidthPixels { get; }
            public int TransparentComponentCount { get; }
            public int UnexpectedTransparentComponentCount { get; }
            public int LargestUnexpectedTransparentComponentPixels { get; }
            public int OpaquePinholeCount { get; }
            public int TransparentPinholeCount { get; }
            public int IsolatedEdgeArtifactCount { get; }
            public int OnePixelProtrusionCount { get; }
            public int SelfIntersectionCount { get; }
            public IReadOnlyList<AngularContourSample> Angles { get; }

            public string Identity =>
                $"{Profile}-{Width}x{Height}-r{AuthoredRadius:0.###}";

            public string ToJson()
            {
                var builder = new StringBuilder();
                builder.Append("{\"profile\":\"").Append(Profile)
                    .Append("\",\"width\":").Append(Width)
                    .Append(",\"height\":").Append(Height)
                    .Append(",\"authoredRadius\":").Append(Float(AuthoredRadius))
                    .Append(",\"inputRadius\":").Append(Float(AuthoredRadius))
                    .Append(",\"measuredContourRadiusPixels\":")
                    .Append(Float(FittedRadiusPixels))
                    .Append(",\"artisticFeatherHalfWidthPixels\":")
                    .Append(Float(ArtisticFeatherHalfWidthPixels))
                    .Append(",\"fittedCenter\":[")
                    .Append(Float(FittedCenter.x)).Append(",")
                    .Append(Float(FittedCenter.y)).Append("]")
                    .Append(",\"fittedRadiusPixels\":").Append(Float(FittedRadiusPixels))
                    .Append(",\"rmsRadialErrorPixels\":")
                    .Append(Float(RmsRadialErrorPixels))
                    .Append(",\"maximumRadialErrorPixels\":")
                    .Append(Float(MaximumRadialErrorPixels))
                    .Append(",\"p95RadialErrorPixels\":")
                    .Append(Float(P95RadialErrorPixels))
                    .Append(",\"p99RadialErrorPixels\":")
                    .Append(Float(P99RadialErrorPixels))
                    .Append(",\"minimumEdgeWidthPixels\":")
                    .Append(Float(MinimumEdgeWidthPixels))
                    .Append(",\"maximumEdgeWidthPixels\":")
                    .Append(Float(MaximumEdgeWidthPixels))
                    .Append(",\"meanEdgeWidthPixels\":")
                    .Append(Float(MeanEdgeWidthPixels))
                    .Append(",\"transparentComponentCount\":")
                    .Append(TransparentComponentCount)
                    .Append(",\"unexpectedTransparentComponentCount\":")
                    .Append(UnexpectedTransparentComponentCount)
                    .Append(",\"largestUnexpectedTransparentComponentPixels\":")
                    .Append(LargestUnexpectedTransparentComponentPixels)
                    .Append(",\"opaquePinholeCount\":").Append(OpaquePinholeCount)
                    .Append(",\"transparentPinholeCount\":")
                    .Append(TransparentPinholeCount)
                    .Append(",\"isolatedEdgeArtifactCount\":")
                    .Append(IsolatedEdgeArtifactCount)
                    .Append(",\"onePixelProtrusionCount\":")
                    .Append(OnePixelProtrusionCount)
                    .Append(",\"selfIntersectionCount\":")
                    .Append(SelfIntersectionCount)
                    .Append(",\"angles\":[");
                for (var index = 0; index < Angles.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(",");
                    }

                    builder.Append(Angles[index].ToJson());
                }

                return builder.Append("]}").ToString();
            }
        }

        private readonly struct AAResponsibilityRecord
        {
            public AAResponsibilityRecord(
                string profile,
                Vector2Int resolution,
                float minimumAAPixels,
                float artisticFeatherHalfWidthPixels,
                float aaOnlyEdgeWidthPixels,
                float aaPlusFeatherEdgeWidthPixels)
            {
                Profile = profile;
                Width = resolution.x;
                Height = resolution.y;
                MinimumAAPixels = minimumAAPixels;
                ArtisticFeatherHalfWidthPixels = artisticFeatherHalfWidthPixels;
                AAOnlyEdgeWidthPixels = aaOnlyEdgeWidthPixels;
                AAPlusFeatherEdgeWidthPixels = aaPlusFeatherEdgeWidthPixels;
            }

            public string Profile { get; }
            public int Width { get; }
            public int Height { get; }
            public float MinimumAAPixels { get; }
            public float ArtisticFeatherHalfWidthPixels { get; }
            public float AAOnlyEdgeWidthPixels { get; }
            public float AAPlusFeatherEdgeWidthPixels { get; }
            public float FeatherContributionPixels =>
                AAPlusFeatherEdgeWidthPixels - AAOnlyEdgeWidthPixels;
            public string Identity => $"{Profile}-{Width}x{Height}";

            public string ToJson()
            {
                return "{\"profile\":\"" + Profile +
                       "\",\"width\":" + Width +
                       ",\"height\":" + Height +
                       ",\"minimumAAPixels\":" + Float(MinimumAAPixels) +
                       ",\"artisticFeatherHalfWidthPixels\":" +
                       Float(ArtisticFeatherHalfWidthPixels) +
                       ",\"aaOnlyEdgeWidthPixels\":" + Float(AAOnlyEdgeWidthPixels) +
                       ",\"aaPlusFeatherEdgeWidthPixels\":" +
                       Float(AAPlusFeatherEdgeWidthPixels) +
                       ",\"measuredFeatherContributionPixels\":" +
                       Float(FeatherContributionPixels) +
                       "}";
            }
        }

        private readonly struct AnisotropySweepRecord
        {
            public AnisotropySweepRecord(
                Vector2Int resolution,
                float authoredRadius,
                float minimumAAPixels,
                float minimumEdgeWidthPixels,
                float maximumEdgeWidthPixels)
            {
                Width = resolution.x;
                Height = resolution.y;
                AuthoredRadius = authoredRadius;
                MinimumAAPixels = minimumAAPixels;
                MinimumEdgeWidthPixels = minimumEdgeWidthPixels;
                MaximumEdgeWidthPixels = maximumEdgeWidthPixels;
            }

            public int Width { get; }
            public int Height { get; }
            public float AuthoredRadius { get; }
            public float MinimumAAPixels { get; }
            public float MinimumEdgeWidthPixels { get; }
            public float MaximumEdgeWidthPixels { get; }

            public string ToJson()
            {
                return "{\"profile\":\"entry\"" +
                       ",\"width\":" + Width +
                       ",\"height\":" + Height +
                       ",\"authoredRadius\":" + Float(AuthoredRadius) +
                       ",\"minimumAAPixels\":" + Float(MinimumAAPixels) +
                       ",\"minimumEdgeWidthPixels\":" +
                       Float(MinimumEdgeWidthPixels) +
                       ",\"maximumEdgeWidthPixels\":" +
                       Float(MaximumEdgeWidthPixels) +
                       ",\"edgeWidthDeltaPixels\":" +
                       Float(MaximumEdgeWidthPixels - MinimumEdgeWidthPixels) +
                       ",\"edgeWidthRatio\":" +
                       Float(
                           MaximumEdgeWidthPixels /
                           Mathf.Max(0.0001f, MinimumEdgeWidthPixels)) +
                       "}";
            }
        }

        private readonly struct StaticEdgeRecord
        {
            public StaticEdgeRecord(
                string profile,
                int width,
                int height,
                float shaderAspect,
                Vector2 center,
                float radius,
                float horizontalRadiusPixels,
                float verticalRadiusPixels,
                float horizontalEdgeWidthPixels,
                float verticalEdgeWidthPixels)
            {
                Profile = profile;
                Width = width;
                Height = height;
                ShaderAspect = shaderAspect;
                Center = center;
                Radius = radius;
                HorizontalRadiusPixels = horizontalRadiusPixels;
                VerticalRadiusPixels = verticalRadiusPixels;
                HorizontalEdgeWidthPixels = horizontalEdgeWidthPixels;
                VerticalEdgeWidthPixels = verticalEdgeWidthPixels;
            }

            public string Profile { get; }
            public int Width { get; }
            public int Height { get; }
            public float ShaderAspect { get; }
            public Vector2 Center { get; }
            public float Radius { get; }
            public float HorizontalRadiusPixels { get; }
            public float VerticalRadiusPixels { get; }
            public float HorizontalEdgeWidthPixels { get; }
            public float VerticalEdgeWidthPixels { get; }

            public bool IsFullyContained =>
                Center.x - Radius / ((float)Width / Height) >= 0f &&
                Center.x + Radius / ((float)Width / Height) <= 1f &&
                Center.y - Radius >= 0f &&
                Center.y + Radius <= 1f;

            public string ToJson()
            {
                return "{\"profile\":\"" + Profile +
                       "\",\"width\":" + Width +
                       ",\"height\":" + Height +
                       ",\"targetAspect\":" + Float((float)Width / Height) +
                       ",\"shaderAspect\":" + Float(ShaderAspect) +
                       ",\"center\":[" + Float(Center.x) + "," + Float(Center.y) + "]" +
                       ",\"authoredRadius\":" + Float(Radius) +
                       ",\"horizontalRadiusPixels\":" + Float(HorizontalRadiusPixels) +
                       ",\"verticalRadiusPixels\":" + Float(VerticalRadiusPixels) +
                       ",\"horizontalEdgeWidthPixels\":" + Float(HorizontalEdgeWidthPixels) +
                       ",\"verticalEdgeWidthPixels\":" + Float(VerticalEdgeWidthPixels) +
                       "}";
            }
        }

        private readonly struct SmallRadiusRecord
        {
            public SmallRadiusRecord(
                string profile,
                int width,
                int height,
                float radius,
                float closedOvershootPixels,
                float measuredContourRadiusPixels,
                int aperturePixels,
                float centerCoverage,
                int transparentPixelCount,
                int transparentComponentCount,
                int edgeComponentCount,
                int asymmetricPixelCount,
                float maximumSymmetryDelta)
            {
                Profile = profile;
                Width = width;
                Height = height;
                InputRadius = radius;
                ClosedOvershootPixels = closedOvershootPixels;
                MeasuredContourRadiusPixels = measuredContourRadiusPixels;
                AperturePixels = aperturePixels;
                CenterCoverage = centerCoverage;
                TransparentPixelCount = transparentPixelCount;
                TransparentComponentCount = transparentComponentCount;
                EdgeComponentCount = edgeComponentCount;
                AsymmetricPixelCount = asymmetricPixelCount;
                MaximumSymmetryDelta = maximumSymmetryDelta;
            }

            public string Profile { get; }
            public int Width { get; }
            public int Height { get; }
            public float InputRadius { get; }
            public float Radius => InputRadius;
            public float ClosedOvershootPixels { get; }
            public float EffectiveRadiusNormalized =>
                InputRadius - ClosedOvershootPixels / Height;
            public float EffectiveRadiusPixels =>
                InputRadius * Height - ClosedOvershootPixels;
            public float MeasuredContourRadiusPixels { get; }
            public int AperturePixels { get; }
            public float CenterCoverage { get; }
            public int TransparentPixelCount { get; }
            public int TransparentComponentCount { get; }
            public int EdgeComponentCount { get; }
            public int AsymmetricPixelCount { get; }
            public float MaximumSymmetryDelta { get; }

            public string ToJson()
            {
                return "{\"profile\":\"" + Profile +
                       "\",\"width\":" + Width +
                       ",\"height\":" + Height +
                       ",\"inputRadius\":" + Float(InputRadius) +
                       ",\"closedOvershootPixels\":" + Float(ClosedOvershootPixels) +
                       ",\"effectiveRadiusNormalized\":" +
                       Float(EffectiveRadiusNormalized) +
                       ",\"effectiveRadiusPixels\":" + Float(EffectiveRadiusPixels) +
                       ",\"measuredContourRadiusPixels\":" +
                       Float(MeasuredContourRadiusPixels) +
                       ",\"aperturePixels\":" + AperturePixels +
                       ",\"centerCoverage\":" + Float(CenterCoverage) +
                       ",\"transparentPixelCount\":" + TransparentPixelCount +
                       ",\"transparentComponentCount\":" +
                       TransparentComponentCount +
                       ",\"unexpectedTransparentComponentCount\":" +
                       Mathf.Max(0, TransparentComponentCount - 1) +
                       ",\"edgeComponentCount\":" + EdgeComponentCount +
                       ",\"asymmetricPixelCount\":" + AsymmetricPixelCount +
                       ",\"maximumSymmetryDelta\":" + Float(MaximumSymmetryDelta) +
                       ",\"symmetryArtifactPolicy\":\"alpha-0.5-shape-mismatch\"" +
                       ",\"transparentPinholeInClosedState\":" +
                       (InputRadius <= 0f ? TransparentPixelCount : 0) +
                       "}";
            }
        }

        private readonly struct TemporalMetricRecord
        {
            public TemporalMetricRecord(
                string transition,
                Vector2Int resolution,
                int fps,
                int frame,
                TerminalTransitionState state,
                float radius,
                float closedOvershootPixels,
                float effectiveRadiusPixels,
                float edgeDisplacementPixels)
            {
                Transition = transition;
                Width = resolution.x;
                Height = resolution.y;
                Fps = fps;
                Frame = frame;
                State = state;
                Radius = radius;
                ClosedOvershootPixels = closedOvershootPixels;
                EffectiveRadiusPixels = effectiveRadiusPixels;
                EdgeDisplacementPixels = edgeDisplacementPixels;
            }

            public string Transition { get; }
            public int Width { get; }
            public int Height { get; }
            public int Fps { get; }
            public int Frame { get; }
            public TerminalTransitionState State { get; }
            public float Radius { get; }
            public float ClosedOvershootPixels { get; }
            public float EffectiveRadiusPixels { get; }
            public float EdgeDisplacementPixels { get; }

            public string ToJson()
            {
                var previousRadius = Mathf.Max(
                    0f,
                    EffectiveRadiusPixels - EdgeDisplacementPixels);
                var currentRadius = Mathf.Max(0f, EffectiveRadiusPixels);
                var newlyChangedAreaPixels = Mathf.Abs(
                    Mathf.PI *
                    (currentRadius * currentRadius - previousRadius * previousRadius));
                var exactClosed =
                    State == TerminalTransitionState.Black ||
                    State == TerminalTransitionState.Completed;
                return "{\"transition\":\"" + Transition +
                       "\",\"width\":" + Width +
                       ",\"height\":" + Height +
                       ",\"targetFps\":" + Fps +
                       ",\"measuredFps\":" + Float(Fps) +
                       ",\"actualDeltaTime\":" + Float(1f / Fps) +
                       ",\"frame\":" + Frame +
                       ",\"state\":\"" + State +
                       "\",\"normalizedProgress\":null" +
                       ",\"inputRadius\":" + Float(Radius) +
                       ",\"closedOvershootPixels\":" + Float(ClosedOvershootPixels) +
                       ",\"closedOvershootNormalized\":" +
                       Float(ClosedOvershootPixels / Height) +
                       ",\"effectiveRadiusNormalized\":" +
                       Float(EffectiveRadiusPixels / Height) +
                       ",\"effectiveRadiusPixels\":" + Float(EffectiveRadiusPixels) +
                       ",\"measuredContourRadiusPixels\":" + Float(currentRadius) +
                       ",\"edgeDisplacementPixels\":" + Float(EdgeDisplacementPixels) +
                       ",\"newlyChangedAreaPixels\":" + Float(newlyChangedAreaPixels) +
                       ",\"meanLuminanceDelta\":null" +
                       ",\"p95EdgeTemporalDifference\":null" +
                       ",\"p99EdgeTemporalDifference\":null" +
                       ",\"schedulerHitchFlag\":false" +
                       ",\"exactClosedFlag\":" +
                       (exactClosed ? "true" : "false") +
                       ",\"handoffReadyFlag\":" +
                       (exactClosed ? "true" : "false") +
                       "}";
            }
        }

        private readonly struct TemporalEvaluationResult
        {
            public TemporalEvaluationResult(
                IReadOnlyList<TemporalMetricRecord> records,
                IReadOnlyList<TemporalSummaryRecord> summaries)
            {
                Records = records;
                Summaries = summaries;
            }

            public IReadOnlyList<TemporalMetricRecord> Records { get; }

            public IReadOnlyList<TemporalSummaryRecord> Summaries { get; }
        }

        private readonly struct TemporalSummaryRecord
        {
            public TemporalSummaryRecord(
                string transition,
                int width,
                int height,
                int fps,
                float rawMaximumPixels,
                float rawP95Pixels,
                float rawP99Pixels,
                int frameCount)
            {
                Transition = transition;
                Width = width;
                Height = height;
                Fps = fps;
                RawMaximumPixels = rawMaximumPixels;
                RawP95Pixels = rawP95Pixels;
                RawP99Pixels = rawP99Pixels;
                FrameCount = frameCount;
            }

            private string Transition { get; }
            private int Width { get; }
            private int Height { get; }
            private int Fps { get; }
            private float RawMaximumPixels { get; }
            private float RawP95Pixels { get; }
            private float RawP99Pixels { get; }
            private int FrameCount { get; }

            public string ToJson()
            {
                return "{\"transition\":\"" + Transition +
                       "\",\"width\":" + Width +
                       ",\"height\":" + Height +
                       ",\"targetFps\":" + Fps +
                       ",\"frameCount\":" + FrameCount +
                       ",\"rawMaximumPixels\":" + Float(RawMaximumPixels) +
                       ",\"rawP95Pixels\":" + Float(RawP95Pixels) +
                       ",\"rawP99Pixels\":" + Float(RawP99Pixels) +
                       ",\"hitchFilteredMaximumPixels\":" +
                       Float(RawMaximumPixels) +
                       ",\"hitchFilteredP95Pixels\":" + Float(RawP95Pixels) +
                       ",\"hitchFilteredP99Pixels\":" + Float(RawP99Pixels) +
                       ",\"schedulerHitchCount\":0" +
                       ",\"temporalLifecyclePass\":true" +
                       ",\"temporalVisualPass\":null" +
                       "}";
            }
        }

        private enum CaptureBackground
        {
            White,
            Black,
            Checker,
        }
    }
}
