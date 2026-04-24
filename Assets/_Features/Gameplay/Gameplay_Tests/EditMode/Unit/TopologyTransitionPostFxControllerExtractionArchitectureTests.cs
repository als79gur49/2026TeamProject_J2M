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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    /*
     * Phase 5A controller-extraction guardrail:
     * - this suite protects post-fx extraction, not post-fx redesign
     * - runtime clone and inactive reset assertions freeze extraction behavior only
     * - any representative samples here are phase-local extraction parity, not long-term tuning law
     */
    public sealed class TopologyTransitionPostFxControllerExtractionArchitectureTests
    {
        private const string RuntimeRelativeDirectory = "Assets/_Features/Gameplay/Gameplay_Host/Runtime";
        private const string ControllerRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PostFx/Controllers/TopologyTransitionPostFxController.cs";
        private const string ControllerMetaRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PostFx/Controllers/TopologyTransitionPostFxController.cs.meta";
        private const string LegacyControllerRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyTransitionPostFxController.cs";
        private const string ControllersRelativeDirectory =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PostFx/Controllers";
        private const string ControllersFolderMetaRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PostFx/Controllers.meta";
        private const string ExpectedControllerGuid = "9a2d7a7dd8a34ae2b1cf2b268af1a7f4";

        private static readonly string[] ForbiddenControllerIdentifiers =
        {
            "GameplayTickViewPresenter",
            "GameplayHostRuntimeFactory",
            "GameplayShowcaseSceneScaffold",
            "GameplayCameraRig",
            "TopologyTransitionCameraShakeController",
        };

        private static readonly string[] ForbiddenLookupFragments =
        {
            "FindObjectOfType(",
            "FindObjectsOfType(",
            "FindFirstObjectByType(",
            "FindAnyObjectByType(",
            "GameObject.Find(",
            "Camera.main",
            "SceneManager.",
            "Resources.FindObjectsOfTypeAll(",
        };

        [Test]
        [Category("Extended")]
        public void PostFxControllerFile_Exists_InDedicatedPostFxRuntimeControllerLane()
        {
            var controllerSource = ReadRepoFile(ControllerRelativePath);

            Assert.That(controllerSource, Does.Contain("public sealed class TopologyTransitionPostFxController"));
            Assert.That(controllerSource, Does.Contain("namespace Game.Feature.Gameplay.Host"));
        }

        [Test]
        [Category("Extended")]
        public void PostFxRuntimeControllerFolder_Exists_WithTrackedMetaFile()
        {
            Assert.That(Directory.Exists(GetAbsolutePath(ControllersRelativeDirectory)), Is.True);
            Assert.That(File.Exists(GetAbsolutePath(ControllersFolderMetaRelativePath)), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void PostFxControllerMetaGuid_RemainsUnchanged_AfterMove()
        {
            var metaSource = ReadRepoFile(ControllerMetaRelativePath);

            Assert.That(metaSource, Does.Contain($"guid: {ExpectedControllerGuid}"));
        }

        [Test]
        [Category("Extended")]
        public void LegacyTopLevelPostFxControllerFile_IsAbsent_AfterExtraction()
        {
            Assert.That(File.Exists(GetAbsolutePath(LegacyControllerRelativePath)), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void RuntimeDirectory_ContainsSingleDeclaration_ForTopologyTransitionPostFxController()
        {
            var runtimeSource = ReadCombinedSource(RuntimeRelativeDirectory);

            Assert.That(
                CountMatches(runtimeSource, @"\bclass\s+TopologyTransitionPostFxController\b"),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeTopLevelFiles_DoNotReintroduce_TopologyTransitionPostFxControllerDeclaration()
        {
            var topLevelFiles = Directory.GetFiles(GetAbsolutePath(RuntimeRelativeDirectory), "*.cs", SearchOption.TopDirectoryOnly);

            foreach (var filePath in topLevelFiles)
            {
                var fileSource = File.ReadAllText(filePath).Replace("\r\n", "\n");
                Assert.That(
                    Regex.IsMatch(
                        fileSource,
                        @"\bclass\s+TopologyTransitionPostFxController\b",
                        RegexOptions.CultureInvariant),
                    Is.False,
                    $"Unexpected top-level declaration in '{filePath}'.");
            }
        }

        [Test]
        [Category("Extended")]
        public void PostFxControllerSource_DoesNotReference_ForbiddenDependencies_OrSceneGlobalLookups()
        {
            var controllerSource = ReadRepoFile(ControllerRelativePath);
            var sanitizedSource = StripCommentsAndStringLiterals(controllerSource);
            var identifierTokens = GetIdentifierTokens(sanitizedSource);

            foreach (var forbiddenIdentifier in ForbiddenControllerIdentifiers)
            {
                Assert.That(
                    identifierTokens.Contains(forbiddenIdentifier),
                    Is.False,
                    $"Forbidden dependency '{forbiddenIdentifier}' was found in the controller source.");
            }

            foreach (var forbiddenFragment in ForbiddenLookupFragments)
            {
                Assert.That(
                    controllerSource.Contains(forbiddenFragment),
                    Is.False,
                    $"Forbidden scene-global lookup fragment '{forbiddenFragment}' was found in the controller source.");
            }
        }

        [Test]
        [Category("Extended")]
        public void PostFxControllerInitializeAndApplySignatures_CloseOverProfile_VisualState_AndCamera()
        {
            var initializeMethod = typeof(TopologyTransitionPostFxController).GetMethod(
                nameof(TopologyTransitionPostFxController.Initialize),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            var applyMethod = typeof(TopologyTransitionPostFxController).GetMethod(
                nameof(TopologyTransitionPostFxController.Apply),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            Assert.That(initializeMethod, Is.Not.Null);
            Assert.That(initializeMethod?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(
                initializeMethod?.GetParameters().Select(parameter => parameter.ParameterType).ToArray(),
                Is.EqualTo(new[]
                {
                    typeof(TopologyTransitionPostFxProfile),
                    typeof(Camera),
                }));

            Assert.That(applyMethod, Is.Not.Null);
            Assert.That(applyMethod?.ReturnType, Is.EqualTo(typeof(void)));

            var applyParameters = applyMethod?.GetParameters() ?? Array.Empty<ParameterInfo>();
            Assert.That(applyParameters.Length, Is.EqualTo(1));
            Assert.That(applyParameters[0].ParameterType.IsByRef, Is.True);
            Assert.That(applyParameters[0].ParameterType.GetElementType(), Is.EqualTo(typeof(TopologyTransitionVisualState)));
        }

        [Test]
        [Category("Extended")]
        public void PostFxControllerSource_UsesRuntimeClonePath_AndDoesNotMutateSourceProfileDirectly()
        {
            var controllerSource = ReadRepoFile(ControllerRelativePath);

            Assert.That(controllerSource, Does.Contain("ScriptableObject.CreateInstance<VolumeProfile>()"));
            Assert.That(controllerSource, Does.Contain("runtimeProfile.components.Add(Instantiate(component));"));
            Assert.That(controllerSource, Does.Contain("_runtimeVolume.sharedProfile = sourceProfile;"));
            Assert.That(controllerSource, Does.Contain("_runtimeVolume.profile = _runtimeVolumeProfile;"));
            Assert.That(controllerSource, Does.Not.Contain("sourceProfile.Add<"));
            Assert.That(controllerSource, Does.Not.Contain("sourceProfile.Reset("));
        }

        [Test]
        [Category("Extended")]
        public void PostFxController_RuntimeClonePath_DoesNotMutateAuthoritativeSourceProfile()
        {
            var controllerObject = new GameObject(nameof(PostFxController_RuntimeClonePath_DoesNotMutateAuthoritativeSourceProfile));
            var cameraObject = new GameObject("TopologyTransitionPostFxController_OutputCamera");
            var sourceProfile = ScriptableObject.CreateInstance<VolumeProfile>();

            try
            {
                var controller = controllerObject.AddComponent<TopologyTransitionPostFxController>();
                var outputCamera = cameraObject.AddComponent<Camera>();
                var authoritativeMotionBlur = sourceProfile.Add<MotionBlur>(overrides: true);
                var authoritativeLensDistortion = sourceProfile.Add<LensDistortion>(overrides: true);

                authoritativeMotionBlur.intensity.overrideState = true;
                authoritativeMotionBlur.intensity.value = 0.17f;
                authoritativeLensDistortion.intensity.overrideState = true;
                authoritativeLensDistortion.intensity.value = -0.11f;
                authoritativeLensDistortion.scale.overrideState = true;
                authoritativeLensDistortion.scale.value = 1.33f;

                controller.Initialize(
                    TopologyTransitionPostFxProfile.Create(
                        sourceProfile,
                        maxBlurIntensity: 0.5f,
                        distortionProfile: TopologyTransitionDistortionProfile.Create(
                            impactStart01: 0.1f,
                            impactDuration01: 0.2f,
                            impactIntensity: -0.3f,
                            landingStart01: 0.7f,
                            landingDuration01: 0.2f,
                            landingIntensity: 0.15f,
                            xMultiplier: 0.8f,
                            yMultiplier: 0.6f,
                            center: new Vector2(0.45f, 0.55f),
                            scale: 1.08f)),
                    outputCamera);

                controller.Apply(CreateActiveVisualState(progress01: 0.2f, angularVelocityNormalized: 1f));

                Assert.That(controller.RuntimeVolumeProfile, Is.Not.Null);
                Assert.That(controller.RuntimeVolumeProfile, Is.Not.SameAs(sourceProfile));
                Assert.That(controller.RuntimeVolume.sharedProfile, Is.SameAs(sourceProfile));
                Assert.That(controller.RuntimeVolume.profile, Is.SameAs(controller.RuntimeVolumeProfile));
                Assert.That(controller.MotionBlurOverride.intensity.value, Is.GreaterThan(0f));
                Assert.That(Mathf.Abs(controller.LensDistortionOverride.intensity.value), Is.GreaterThan(0f));

                Assert.That(authoritativeMotionBlur.intensity.value, Is.EqualTo(0.17f).Within(0.0001f));
                Assert.That(authoritativeLensDistortion.intensity.value, Is.EqualTo(-0.11f).Within(0.0001f));
                Assert.That(authoritativeLensDistortion.scale.value, Is.EqualTo(1.33f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(sourceProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void PostFxController_ExtractionParityFreeze_InactiveVisualState_ResetsRuntimeIntensitiesToZero()
        {
            var controllerObject = new GameObject(nameof(PostFxController_ExtractionParityFreeze_InactiveVisualState_ResetsRuntimeIntensitiesToZero));
            var cameraObject = new GameObject("TopologyTransitionPostFxController_InactiveResetCamera");
            var sourceProfile = ScriptableObject.CreateInstance<VolumeProfile>();

            try
            {
                var controller = controllerObject.AddComponent<TopologyTransitionPostFxController>();
                var outputCamera = cameraObject.AddComponent<Camera>();
                sourceProfile.Add<MotionBlur>(overrides: true);
                sourceProfile.Add<LensDistortion>(overrides: true);

                controller.Initialize(
                    TopologyTransitionPostFxProfile.Create(
                        sourceProfile,
                        maxBlurIntensity: 0.5f,
                        distortionProfile: TopologyTransitionDistortionProfile.Create(
                            impactStart01: 0.1f,
                            impactDuration01: 0.2f,
                            impactIntensity: -0.3f,
                            landingStart01: 0.7f,
                            landingDuration01: 0.2f,
                            landingIntensity: 0.15f)),
                    outputCamera);

                controller.Apply(CreateActiveVisualState(progress01: 0.2f, angularVelocityNormalized: 1f));

                Assert.That(controller.MotionBlurOverride.intensity.value, Is.GreaterThan(0f));
                Assert.That(Mathf.Abs(controller.LensDistortionOverride.intensity.value), Is.GreaterThan(0f));

                controller.Apply(TopologyTransitionVisualState.Inactive(new CubeTopologyState(FaceId.Front), Quaternion.identity));

                Assert.That(controller.MotionBlurOverride.intensity.value, Is.EqualTo(0f));
                Assert.That(controller.LensDistortionOverride.intensity.value, Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(sourceProfile);
            }
        }

        private static TopologyTransitionVisualState CreateActiveVisualState(float progress01, float angularVelocityNormalized)
        {
            return new TopologyTransitionVisualState(
                isActive: true,
                progress01: progress01,
                sourceTopology: new CubeTopologyState(FaceId.Floor),
                destinationTopology: new CubeTopologyState(FaceId.Front),
                rotationKind: CubeRotationKind.Forward,
                durationSeconds: 0.4f,
                presentedVisualRotation: Quaternion.identity,
                angularVelocityNormalized: angularVelocityNormalized);
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
