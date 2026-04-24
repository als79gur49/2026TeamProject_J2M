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

        private static readonly Vector3 ForwardImpactFrozenLocalPosition = new(
            -0.008915345f,
            -0.005943563f,
            0.013373017f);

        private static readonly Quaternion ForwardImpactFrozenLocalRotation =
            Quaternion.Euler(-0.33432542f, -0.20802471f, 0.08915345f);

        private static readonly Vector3 ForwardLandingFrozenLocalPosition = new(
            0.002459249f,
            -0.001639499f,
            0.004098748f);

        private static readonly Quaternion ForwardLandingFrozenLocalRotation =
            Quaternion.Euler(0.09836994f, 0.05738247f, 0.03278998f);

        private static readonly Vector3 BackwardImpactFrozenLocalPosition = new(
            0.008915345f,
            -0.005943563f,
            0.013373017f);

        private static readonly Quaternion BackwardImpactFrozenLocalRotation =
            Quaternion.Euler(0.33432542f, 0.20802471f, 0.08915345f);

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
        public void GameplayCameraRig_UsesControllerEvaluate_AndNotLegacyStatefulControllerApi()
        {
            var rigSource = ReadRepoFile(GameplayCameraRigRelativePath);

            Assert.That(rigSource, Does.Contain("_topologyTransitionCameraShakeController.Evaluate("));
            Assert.That(rigSource, Does.Not.Contain("_topologyTransitionCameraShakeController.Initialize("));
            Assert.That(rigSource, Does.Not.Contain("_topologyTransitionCameraShakeController.Apply("));
            Assert.That(rigSource, Does.Not.Contain("_topologyTransitionCameraShakeController.Reset("));
            Assert.That(rigSource, Does.Not.Contain("_topologyTransitionCameraShakeController.LocalPosition"));
            Assert.That(rigSource, Does.Not.Contain("_topologyTransitionCameraShakeController.LocalRotation"));
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
        public void GameplayCameraRig_ShakeCache_MirrorsControllerEvaluateResult()
        {
            var rigObject = new GameObject(nameof(GameplayCameraRig_ShakeCache_MirrorsControllerEvaluateResult));

            try
            {
                var rig = rigObject.AddComponent<GameplayCameraRig>();
                var profile = TopologyTransitionCameraShakeProfile.CreateDefault();
                var visualState = CreateActiveVisualState(progress01: 0.12f, CubeRotationKind.Forward);
                var expectedResult = new TopologyTransitionCameraShakeController().Evaluate(visualState, profile);

                GameplayCameraRigReflectionAdapter.ConfigureTopologyTransitionCameraShake(rig, profile);
                GameplayCameraRigReflectionAdapter.ApplyTopologyTransitionVisualState(rig, visualState);

                AssertFrozenLocalPosition(
                    GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalPosition(rig),
                    expectedResult.LocalPosition);
                AssertFrozenLocalRotationPose(
                    GameplayCameraRigReflectionAdapter.GetTopologyTransitionShakeLocalRotation(rig),
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

        [Test]
        [Category("Extended")]
        public void CameraShakeController_ExtractionParityFreeze_ForwardImpactSample_PreservesPoseAtProgress012()
        {
            var result = EvaluateDefaultProfile(progress01: 0.12f, CubeRotationKind.Forward);

            AssertFrozenLocalPosition(result.LocalPosition, ForwardImpactFrozenLocalPosition);
            AssertFrozenLocalRotationPose(result.LocalRotation, ForwardImpactFrozenLocalRotation);
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeController_ExtractionParityFreeze_ForwardLandingSample_PreservesPoseAtProgress084()
        {
            var result = EvaluateDefaultProfile(progress01: 0.84f, CubeRotationKind.Forward);

            AssertFrozenLocalPosition(result.LocalPosition, ForwardLandingFrozenLocalPosition);
            AssertFrozenLocalRotationPose(result.LocalRotation, ForwardLandingFrozenLocalRotation);
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeController_ExtractionParityFreeze_OutsideEnvelopeSample_ReturnsZeroPoseDeltaAtProgress050()
        {
            var result = EvaluateDefaultProfile(progress01: 0.50f, CubeRotationKind.Forward);

            Assert.That(result.LocalPosition, Is.EqualTo(Vector3.zero));
            AssertFrozenLocalRotationPose(result.LocalRotation, Quaternion.identity);
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeController_ExtractionParityFreeze_BackwardImpactSample_PreservesDirectionSignPoseAtProgress012()
        {
            var result = EvaluateDefaultProfile(progress01: 0.12f, CubeRotationKind.Backward);

            AssertFrozenLocalPosition(result.LocalPosition, BackwardImpactFrozenLocalPosition);
            AssertFrozenLocalRotationPose(result.LocalRotation, BackwardImpactFrozenLocalRotation);
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

        private static class GameplayCameraRigReflectionAdapter
        {
            private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            private static readonly MethodInfo ConfigureTopologyTransitionCameraShakeMethod =
                GetRequiredMethod("ConfigureTopologyTransitionCameraShake", parameterCount: 1);
            private static readonly MethodInfo ApplyTopologyTransitionVisualStateMethod =
                GetRequiredMethod("ApplyTopologyTransitionVisualState", parameterCount: 1);
            private static readonly PropertyInfo TopologyTransitionShakeLocalPositionProperty =
                GetRequiredProperty("TopologyTransitionShakeLocalPosition");
            private static readonly PropertyInfo TopologyTransitionShakeLocalRotationProperty =
                GetRequiredProperty("TopologyTransitionShakeLocalRotation");

            public static void ConfigureTopologyTransitionCameraShake(
                GameplayCameraRig rig,
                TopologyTransitionCameraShakeProfile profile)
            {
                InvokeRequired(ConfigureTopologyTransitionCameraShakeMethod, rig, profile);
            }

            public static void ApplyTopologyTransitionVisualState(
                GameplayCameraRig rig,
                TopologyTransitionVisualState visualState)
            {
                InvokeRequired(ApplyTopologyTransitionVisualStateMethod, rig, visualState);
            }

            public static Vector3 GetTopologyTransitionShakeLocalPosition(GameplayCameraRig rig)
            {
                return (Vector3)GetRequiredValue(TopologyTransitionShakeLocalPositionProperty, rig);
            }

            public static Quaternion GetTopologyTransitionShakeLocalRotation(GameplayCameraRig rig)
            {
                return (Quaternion)GetRequiredValue(TopologyTransitionShakeLocalRotationProperty, rig);
            }

            private static MethodInfo GetRequiredMethod(string name, int parameterCount)
            {
                return typeof(GameplayCameraRig)
                    .GetMethods(InstanceFlags)
                    .Single(method => method.Name == name && method.GetParameters().Length == parameterCount);
            }

            private static PropertyInfo GetRequiredProperty(string name)
            {
                return typeof(GameplayCameraRig).GetProperty(name, InstanceFlags) ??
                       throw new InvalidOperationException($"Missing internal GameplayCameraRig property '{name}'.");
            }

            private static object GetRequiredValue(PropertyInfo property, GameplayCameraRig rig)
            {
                if (rig == null)
                {
                    throw new ArgumentNullException(nameof(rig));
                }

                return property.GetValue(rig) ??
                       throw new InvalidOperationException($"Property '{property.Name}' returned null unexpectedly.");
            }

            private static void InvokeRequired(MethodInfo method, GameplayCameraRig rig, object argument)
            {
                if (rig == null)
                {
                    throw new ArgumentNullException(nameof(rig));
                }

                method.Invoke(rig, new[] { argument });
            }
        }
    }
}
