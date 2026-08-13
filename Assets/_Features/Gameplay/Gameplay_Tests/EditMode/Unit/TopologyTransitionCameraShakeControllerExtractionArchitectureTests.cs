using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    /*
     * Phase 4A controller-extraction guardrail:
     * - this suite protects controller extraction, not shake redesign
     * - frozen samples are extraction-parity freeze and may change with intentional shake tuning
     * - rotation parity uses quaternion pose equivalence as the primary oracle; Euler output is diagnostic only
     * - Phase 4A sign-off is phase-local to this suite plus touched-cluster no-new-regression, not full-suite green
     */
    public sealed class TopologyTransitionCameraShakeControllerExtractionArchitectureTests
    {
        private const string RuntimeRelativeDirectory = "Assets/_Features/Gameplay/Gameplay_Host/Runtime";
        private const string ControllerRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Camera/Controllers/TopologyTransitionCameraShakeController.cs";
        private const string ControllerMetaRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Camera/Controllers/TopologyTransitionCameraShakeController.cs.meta";
        private const string LegacyControllerRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyTransitionCameraShakeController.cs";
        private const string ControllersRelativeDirectory =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Camera/Controllers";
        private const string ControllersFolderMetaRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Camera/Controllers.meta";
        private const string ResultRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Camera/Controllers/TopologyTransitionCameraShakeResult.cs";
        private const string GameplayCameraRigRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraRig.cs";
        private const string GameplayTickViewPresenterRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs";
        private const string ExpectedControllerGuid = "168be352a4c7470291e0f330546b74c9";

        // Extraction-parity float stability tolerance; this is not a gameplay tuning threshold.
        private const float PositionExtractionFreezeTolerance = 0.00001f;

        // Extraction-parity pose equivalence tolerance in degrees; this is not a gameplay tuning threshold.
        private const float RotationPoseToleranceDegrees = 0.001f;

        private static readonly string[] ForbiddenControllerIdentifiers =
        {
            "GameplayTickViewPresenter",
            "GameplayHostRuntimeFactory",
            "GameplayShowcaseSceneScaffold",
            "Random",
            "Time",
        };

        private static readonly string[] RigEnvelopeFieldNames =
        {
            nameof(TopologyTransitionCameraShakeProfile.ImpactStart01),
            nameof(TopologyTransitionCameraShakeProfile.ImpactDuration01),
            nameof(TopologyTransitionCameraShakeProfile.ImpactOscillationCycles),
            nameof(TopologyTransitionCameraShakeProfile.ImpactLocalPositionAmplitude),
            nameof(TopologyTransitionCameraShakeProfile.ImpactLocalRotationAmplitudeDegrees),
            nameof(TopologyTransitionCameraShakeProfile.LandingStart01),
            nameof(TopologyTransitionCameraShakeProfile.LandingDuration01),
            nameof(TopologyTransitionCameraShakeProfile.LandingOscillationCycles),
            nameof(TopologyTransitionCameraShakeProfile.LandingLocalPositionAmplitude),
            nameof(TopologyTransitionCameraShakeProfile.LandingLocalRotationAmplitudeDegrees),
        };

        public readonly struct FrozenWaveformSample
        {
            internal FrozenWaveformSample(
                float progress01,
                Vector3 forwardPosition,
                Vector3 forwardRotationEulerDegrees,
                Vector3 backwardPosition,
                Vector3 backwardRotationEulerDegrees,
                string expectedPulse)
            {
                Progress01 = progress01;
                ForwardPosition = forwardPosition;
                ForwardRotationEulerDegrees = forwardRotationEulerDegrees;
                BackwardPosition = backwardPosition;
                BackwardRotationEulerDegrees = backwardRotationEulerDegrees;
                ExpectedPulse = expectedPulse;
            }

            internal float Progress01 { get; }
            internal Vector3 ForwardPosition { get; }
            internal Vector3 ForwardRotationEulerDegrees { get; }
            internal Vector3 BackwardPosition { get; }
            internal Vector3 BackwardRotationEulerDegrees { get; }
            internal string ExpectedPulse { get; }

            public override string ToString()
            {
                return $"Progress={Progress01:0.000}_{ExpectedPulse}";
            }
        }

        private static readonly FrozenWaveformSample[] FrozenWaveformSamples =
        {
            new(0.000f, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, "None"),
            new(0.020f, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, "ImpactBoundary"),
            new(0.050f,
                new Vector3(0.001500000f, 0.001000000f, -0.002250000f),
                new Vector3(0.056250000f, 0.035000000f, -0.015000000f),
                new Vector3(-0.001500000f, 0.001000000f, -0.002250000f),
                new Vector3(-0.056250000f, -0.035000000f, -0.015000000f),
                "Impact"),
            new(0.110f,
                new Vector3(-0.012000000f, -0.008000000f, 0.018000000f),
                new Vector3(-0.450000000f, -0.280000000f, 0.120000000f),
                new Vector3(0.012000000f, -0.008000000f, 0.018000000f),
                new Vector3(0.450000000f, 0.280000000f, 0.120000000f),
                "Impact"),
            new(0.180f,
                new Vector3(-0.000243756f, -0.000162504f, 0.000365634f),
                new Vector3(-0.009140840f, -0.005687634f, 0.002437557f),
                new Vector3(0.000243756f, -0.000162504f, 0.000365634f),
                new Vector3(0.009140840f, 0.005687634f, 0.002437557f),
                "Impact"),
            new(0.500f, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, "None"),
            new(0.760f, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, "LandingBoundary"),
            new(0.820f,
                new Vector3(-0.001164686f, 0.000776457f, -0.001941143f),
                new Vector3(-0.046587428f, -0.027176000f, -0.015529143f),
                new Vector3(0.001164686f, 0.000776457f, -0.001941143f),
                new Vector3(0.046587428f, 0.027176000f, -0.015529143f),
                "Landing"),
            new(0.900f,
                new Vector3(0.001421928f, -0.000947952f, 0.002369880f),
                new Vector3(0.056877112f, 0.033178315f, 0.018959037f),
                new Vector3(-0.001421928f, -0.000947952f, 0.002369880f),
                new Vector3(-0.056877112f, -0.033178315f, 0.018959037f),
                "Landing"),
            new(0.940f, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, "LandingBoundary"),
            new(1.000f, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, "None"),
        };

        [Test]
        [Category("Extended")]
        public void CameraShakeControllerFile_Exists_InDedicatedCameraRuntimeControllerLane()
        {
            var controllerSource = ReadRepoFile(ControllerRelativePath);

            Assert.That(controllerSource, Does.Contain("public sealed class TopologyTransitionCameraShakeController"));
            Assert.That(controllerSource, Does.Contain("namespace Game.Feature.Gameplay.Host"));
        }

        [Test]
        [Category("Extended")]
        public void CameraRuntimeControllerFolder_Exists_WithTrackedMetaFile()
        {
            Assert.That(Directory.Exists(GetAbsolutePath(ControllersRelativeDirectory)), Is.True);
            Assert.That(File.Exists(GetAbsolutePath(ControllersFolderMetaRelativePath)), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeControllerMetaGuid_RemainsUnchanged_AfterMove()
        {
            var metaSource = ReadRepoFile(ControllerMetaRelativePath);

            Assert.That(metaSource, Does.Contain($"guid: {ExpectedControllerGuid}"));
        }

        [Test]
        [Category("Extended")]
        public void LegacyTopLevelCameraShakeControllerFile_IsAbsent_AfterExtraction()
        {
            Assert.That(File.Exists(GetAbsolutePath(LegacyControllerRelativePath)), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void RuntimeDirectory_ContainsSingleDeclaration_ForCameraShakeController()
        {
            var runtimeSource = ReadCombinedSource(RuntimeRelativeDirectory);

            Assert.That(
                CountMatches(runtimeSource, @"\bclass\s+TopologyTransitionCameraShakeController\b"),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeTopLevelFiles_DoNotReintroduce_CameraShakeControllerDeclaration()
        {
            var topLevelFiles = Directory.GetFiles(GetAbsolutePath(RuntimeRelativeDirectory), "*.cs", SearchOption.TopDirectoryOnly);

            foreach (var filePath in topLevelFiles)
            {
                var fileSource = File.ReadAllText(filePath).Replace("\r\n", "\n");
                Assert.That(
                    Regex.IsMatch(
                        fileSource,
                        @"\bclass\s+TopologyTransitionCameraShakeController\b",
                        RegexOptions.CultureInvariant),
                    Is.False,
                    $"Unexpected top-level declaration in '{filePath}'.");
            }
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeControllerSource_DoesNotReference_ForbiddenDependencies()
        {
            var sanitizedSource = StripCommentsAndStringLiterals(ReadRepoFile(ControllerRelativePath));
            var identifierTokens = GetIdentifierTokens(sanitizedSource);

            foreach (var forbiddenIdentifier in ForbiddenControllerIdentifiers)
            {
                Assert.That(
                    identifierTokens.Contains(forbiddenIdentifier),
                    Is.False,
                    $"Forbidden dependency '{forbiddenIdentifier}' was found in the controller source.");
            }

            Assert.That(
                identifierTokens.Any(identifier => identifier.StartsWith("TopologyTransitionPostFx", StringComparison.Ordinal)),
                Is.False,
                "TopologyTransition camera shake extraction must remain isolated from TopologyTransitionPostFx dependencies.");
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeControllerEvaluateSignature_ClosesOverVisualState_Profile_AndResult()
        {
            var evaluateMethod = typeof(TopologyTransitionCameraShakeController).GetMethod(
                "Evaluate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            Assert.That(evaluateMethod, Is.Not.Null);
            Assert.That(evaluateMethod?.ReturnType, Is.EqualTo(typeof(TopologyTransitionCameraShakeResult)));

            var parameters = evaluateMethod?.GetParameters() ?? Array.Empty<ParameterInfo>();
            Assert.That(parameters.Length, Is.EqualTo(2));
            Assert.That(parameters[0].ParameterType.IsByRef, Is.True);
            Assert.That(parameters[0].ParameterType.GetElementType(), Is.EqualTo(typeof(TopologyTransitionVisualState)));
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(TopologyTransitionCameraShakeProfile)));
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeResult_RemainsPureRuntimeHelperValueObject()
        {
            var resultType = typeof(TopologyTransitionCameraShakeResult);
            var resultSource = ReadRepoFile(ResultRelativePath);
            var fieldTypes = resultType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.FieldType)
                .ToArray();
            var propertyTypes = resultType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(property => property.PropertyType)
                .ToArray();

            Assert.That(resultType.IsPublic, Is.True);
            Assert.That(resultType.IsValueType, Is.True);
            Assert.That(
                Regex.IsMatch(
                    resultSource,
                    @"public\s+readonly\s+struct\s+TopologyTransitionCameraShakeResult",
                    RegexOptions.CultureInvariant),
                Is.True);
            Assert.That(resultSource, Does.Contain("namespace Game.Feature.Gameplay.Host"));
            Assert.That(propertyTypes, Has.Member(typeof(Vector3)));
            Assert.That(propertyTypes, Has.Member(typeof(Quaternion)));
            Assert.That(fieldTypes, Has.No.Member(typeof(Transform)));
            Assert.That(fieldTypes, Has.No.Member(typeof(GameObject)));
            Assert.That(fieldTypes, Has.No.Member(typeof(MonoBehaviour)));
            Assert.That(propertyTypes, Has.No.Member(typeof(Transform)));
            Assert.That(propertyTypes, Has.No.Member(typeof(GameObject)));
            Assert.That(propertyTypes, Has.No.Member(typeof(MonoBehaviour)));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_UsesControllerEvaluate_AndRigRemainsSemanticFree()
        {
            var rigSource = ReadRepoFile(GameplayCameraRigRelativePath);
            var presenterSource = ReadRepoFile(GameplayTickViewPresenterRelativePath);

            Assert.That(presenterSource, Does.Contain("_topologyTransitionCameraShakeController.Evaluate("));
            Assert.That(presenterSource, Does.Contain("_cameraAdditivePosePort.ApplyAdditivePose("));
            Assert.That(rigSource, Does.Not.Contain("TopologyTransitionCameraShakeController"));
            Assert.That(rigSource, Does.Not.Contain("TopologyTransitionCameraShakeProfile"));
            Assert.That(rigSource, Does.Not.Contain("TopologyTransitionVisualState"));
            Assert.That(rigSource, Does.Contain("IGameplayCameraAdditivePosePort"));
            Assert.That(rigSource, Does.Contain("ApplyAdditivePose("));
            Assert.That(rigSource, Does.Contain("ResetAdditivePose("));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraRig_DoesNotDuplicate_ShakeEnvelopeFieldInterpretation()
        {
            var rigSource = ReadRepoFile(GameplayCameraRigRelativePath);

            foreach (var fieldName in RigEnvelopeFieldNames)
            {
                Assert.That(
                    rigSource.Contains(fieldName),
                    Is.False,
                    $"GameplayCameraRig must not directly interpret profile field '{fieldName}'.");
            }

            Assert.That(rigSource, Does.Not.Contain("EvaluatePulse("));
            Assert.That(rigSource, Does.Not.Contain("ResolveSignedPositionAmplitude("));
            Assert.That(rigSource, Does.Not.Contain("ResolveSignedRotationAmplitude("));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraRig_SemanticFreeAdditivePose_MirrorsControllerEvaluateResult()
        {
            var rigObject = new GameObject(nameof(GameplayCameraRig_SemanticFreeAdditivePose_MirrorsControllerEvaluateResult));

            try
            {
                var rig = rigObject.AddComponent<GameplayCameraRig>();
                var profile = TopologyTransitionCameraShakeProfile.CreateDefault();
                var visualState = CreateActiveVisualState(progress01: 0.12f, CubeRotationKind.Forward);
                var expectedResult = new TopologyTransitionCameraShakeController().Evaluate(visualState, profile);

                rig.ApplyAdditivePose(expectedResult.LocalPosition, expectedResult.LocalRotation);

                AssertFrozenLocalPosition(
                    rig.AdditiveLocalPosition,
                    expectedResult.LocalPosition);
                AssertFrozenLocalRotationPose(
                    rig.AdditiveLocalRotation,
                    expectedResult.LocalRotation);
            }
            finally
            {
                Object.DestroyImmediate(rigObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeController_ExtractionParityFreeze_InactiveVisualState_ReturnsZeroPoseDelta()
        {
            var result = new TopologyTransitionCameraShakeController().Evaluate(
                TopologyTransitionVisualState.Inactive(new CubeTopologyState(FaceId.Floor), Quaternion.identity),
                TopologyTransitionCameraShakeProfile.CreateDefault());

            Assert.That(result.LocalPosition, Is.EqualTo(Vector3.zero));
            AssertFrozenLocalRotationPose(result.LocalRotation, Quaternion.identity);
        }

        [Category("Extended")]
        [TestCaseSource(nameof(FrozenWaveformSamples))]
        public void CameraShakeController_M0WaveformFreeze_ForwardAndBackwardSamplesMatchGoldenValues(
            FrozenWaveformSample sample)
        {
            var forwardState = CreateActiveVisualState(sample.Progress01, CubeRotationKind.Forward);
            var backwardState = CreateActiveVisualState(sample.Progress01, CubeRotationKind.Backward);
            var forward = new TopologyTransitionCameraShakeController().Evaluate(
                forwardState,
                TopologyTransitionCameraShakeProfile.CreateDefault());
            var backward = new TopologyTransitionCameraShakeController().Evaluate(
                backwardState,
                TopologyTransitionCameraShakeProfile.CreateDefault());

            Assert.That(forwardState.IsActive, Is.True, sample.ExpectedPulse);
            Assert.That(backwardState.IsActive, Is.True, sample.ExpectedPulse);
            AssertFrozenLocalPosition(forward.LocalPosition, sample.ForwardPosition);
            AssertFrozenLocalRotationPose(
                forward.LocalRotation,
                Quaternion.Euler(sample.ForwardRotationEulerDegrees));
            AssertFrozenLocalPosition(backward.LocalPosition, sample.BackwardPosition);
            AssertFrozenLocalRotationPose(
                backward.LocalRotation,
                Quaternion.Euler(sample.BackwardRotationEulerDegrees));
            AssertFinite(forward);
            AssertFinite(backward);
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeController_M0WaveformFreeze_PulseBoundariesAreContinuousAndZero()
        {
            var boundaries = new[] { 0.02f, 0.20f, 0.76f, 0.94f };
            const float boundaryProbe = 0.00001f;

            foreach (var boundary in boundaries)
            {
                foreach (var direction in new[] { CubeRotationKind.Forward, CubeRotationKind.Backward })
                {
                    var before = EvaluateDefaultProfile(boundary - boundaryProbe, direction);
                    var at = EvaluateDefaultProfile(boundary, direction);
                    var after = EvaluateDefaultProfile(boundary + boundaryProbe, direction);

                    Assert.That(at.LocalPosition.sqrMagnitude, Is.LessThan(0.0000000001f), $"{boundary}/{direction}");
                    AssertFrozenLocalRotationPose(at.LocalRotation, Quaternion.identity);
                    Assert.That(before.LocalPosition.magnitude, Is.LessThan(0.00001f), $"before {boundary}/{direction}");
                    Assert.That(after.LocalPosition.magnitude, Is.LessThan(0.00001f), $"after {boundary}/{direction}");
                    AssertFinite(before);
                    AssertFinite(at);
                    AssertFinite(after);
                }
            }
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeController_M0WaveformFreeze_VisualStateNormalizationClampsOutsideProgressToCompletedIdentity()
        {
            var belowRangeState = CreateActiveVisualState(-10f, CubeRotationKind.Forward);
            var aboveRangeState = CreateActiveVisualState(10f, CubeRotationKind.Forward);
            var belowRange = new TopologyTransitionCameraShakeController().Evaluate(
                belowRangeState,
                TopologyTransitionCameraShakeProfile.CreateDefault());
            var aboveRange = new TopologyTransitionCameraShakeController().Evaluate(
                aboveRangeState,
                TopologyTransitionCameraShakeProfile.CreateDefault());

            Assert.That(belowRangeState.Progress01, Is.EqualTo(0f));
            Assert.That(aboveRangeState.Progress01, Is.EqualTo(1f));
            Assert.That(belowRange.LocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(aboveRange.LocalPosition, Is.EqualTo(Vector3.zero));
            AssertFrozenLocalRotationPose(belowRange.LocalRotation, Quaternion.identity);
            AssertFrozenLocalRotationPose(aboveRange.LocalRotation, Quaternion.identity);
            AssertFinite(belowRange);
            AssertFinite(aboveRange);
        }

        private static TopologyTransitionCameraShakeResult EvaluateDefaultProfile(float progress01, CubeRotationKind rotationKind)
        {
            return new TopologyTransitionCameraShakeController().Evaluate(
                CreateActiveVisualState(progress01, rotationKind),
                TopologyTransitionCameraShakeProfile.CreateDefault());
        }

        private static TopologyTransitionVisualState CreateActiveVisualState(float progress01, CubeRotationKind rotationKind)
        {
            return new TopologyTransitionVisualState(
                isActive: true,
                progress01: progress01,
                sourceTopology: new CubeTopologyState(FaceId.Floor),
                destinationTopology: new CubeTopologyState(FaceId.Front),
                rotationKind: rotationKind,
                durationSeconds: 0.2f,
                presentedVisualRotation: Quaternion.Euler(progress01 * 90f, 0f, 0f),
                angularVelocityNormalized: 1f);
        }

        private static void AssertFrozenLocalPosition(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(PositionExtractionFreezeTolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(PositionExtractionFreezeTolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(PositionExtractionFreezeTolerance));
        }

        private static void AssertFrozenLocalRotationPose(Quaternion actual, Quaternion expected)
        {
            var angleDelta = Quaternion.Angle(actual, expected);
            Assert.That(
                angleDelta,
                Is.LessThan(RotationPoseToleranceDegrees),
                $"Expected pose delta within {RotationPoseToleranceDegrees} degrees, but delta was {angleDelta} degrees. " +
                $"Expected Euler={expected.eulerAngles}, Actual Euler={actual.eulerAngles}.");
        }

        private static void AssertFinite(TopologyTransitionCameraShakeResult result)
        {
            Assert.That(float.IsNaN(result.LocalPosition.x) || float.IsInfinity(result.LocalPosition.x), Is.False);
            Assert.That(float.IsNaN(result.LocalPosition.y) || float.IsInfinity(result.LocalPosition.y), Is.False);
            Assert.That(float.IsNaN(result.LocalPosition.z) || float.IsInfinity(result.LocalPosition.z), Is.False);
            Assert.That(float.IsNaN(result.LocalRotation.x) || float.IsInfinity(result.LocalRotation.x), Is.False);
            Assert.That(float.IsNaN(result.LocalRotation.y) || float.IsInfinity(result.LocalRotation.y), Is.False);
            Assert.That(float.IsNaN(result.LocalRotation.z) || float.IsInfinity(result.LocalRotation.z), Is.False);
            Assert.That(float.IsNaN(result.LocalRotation.w) || float.IsInfinity(result.LocalRotation.w), Is.False);
        }

        private static int CountMatches(string source, string pattern)
        {
            return Regex.Matches(source, pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant).Count;
        }

        private static string ReadCombinedSource(string relativeDirectory)
        {
            return string.Join(
                "\n",
                Directory.GetFiles(GetAbsolutePath(relativeDirectory), "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => File.ReadAllText(path).Replace("\r\n", "\n")));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetAbsolutePath(relativePath)).Replace("\r\n", "\n");
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        private static HashSet<string> GetIdentifierTokens(string sanitizedSource)
        {
            return new HashSet<string>(
                Regex.Matches(
                        sanitizedSource,
                        @"\b[_A-Za-z][_A-Za-z0-9]*\b",
                        RegexOptions.CultureInvariant)
                    .Cast<Match>()
                    .Select(match => match.Value),
                StringComparer.Ordinal);
        }

        private static string StripCommentsAndStringLiterals(string source)
        {
            var builder = new StringBuilder(source.Length);
            var inLineComment = false;
            var inBlockComment = false;
            var inString = false;
            var inVerbatimString = false;
            var inCharLiteral = false;

            for (var i = 0; i < source.Length; i++)
            {
                var current = source[i];
                var next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (inLineComment)
                {
                    if (current == '\n')
                    {
                        inLineComment = false;
                        builder.Append('\n');
                    }
                    else
                    {
                        builder.Append(' ');
                    }

                    continue;
                }

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        builder.Append("  ");
                        i++;
                        inBlockComment = false;
                    }
                    else
                    {
                        builder.Append(current == '\n' ? '\n' : ' ');
                    }

                    continue;
                }

                if (inString)
                {
                    if (current == '\\' && next != '\0')
                    {
                        builder.Append("  ");
                        i++;
                        continue;
                    }

                    builder.Append(current == '\n' ? '\n' : ' ');
                    if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (inVerbatimString)
                {
                    if (current == '"' && next == '"')
                    {
                        builder.Append("  ");
                        i++;
                        continue;
                    }

                    builder.Append(current == '\n' ? '\n' : ' ');
                    if (current == '"')
                    {
                        inVerbatimString = false;
                    }

                    continue;
                }

                if (inCharLiteral)
                {
                    if (current == '\\' && next != '\0')
                    {
                        builder.Append("  ");
                        i++;
                        continue;
                    }

                    builder.Append(current == '\n' ? '\n' : ' ');
                    if (current == '\'')
                    {
                        inCharLiteral = false;
                    }

                    continue;
                }

                if (current == '/' && next == '/')
                {
                    builder.Append("  ");
                    i++;
                    inLineComment = true;
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    builder.Append("  ");
                    i++;
                    inBlockComment = true;
                    continue;
                }

                if (current == '@' && next == '"')
                {
                    builder.Append("  ");
                    i++;
                    inVerbatimString = true;
                    continue;
                }

                if (current == '"')
                {
                    builder.Append(' ');
                    inString = true;
                    continue;
                }

                if (current == '\'')
                {
                    builder.Append(' ');
                    inCharLiteral = true;
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

    }
}
