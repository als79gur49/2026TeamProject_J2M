using System;
using System.Collections;
using System.IO;
using Game.Feature.Gameplay.Tests.Support.Unity;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class VisualEvidenceCaptureProbePlayModeTests
    {
        private const int ProbeWidth = 1280;
        private const int ProbeHeight = 720;

        [UnityTest]
        [Category("Full")]
        public IEnumerator DirectCameraCaptureProbe_RendersValidatedPngAndRestoresState()
        {
            RequireGraphicsDevice();
            var outputDirectory = ReadOutputDirectory();
            var root = new GameObject("VisualEvidenceDirectProbeRoot");
            Camera camera = null;
            try
            {
                camera = CreateProbeScene(root.transform, "DirectProbeCamera");
                var originalTarget = camera.targetTexture;
                var artifactPath = Path.Combine(outputDirectory, "probe", "direct", "direct-camera.png");
                var capture = VisualEvidenceFrameCapture.CaptureFrame(
                    camera,
                    artifactPath,
                    ProbeWidth,
                    ProbeHeight);

                Assert.That(camera.targetTexture, Is.SameAs(originalTarget));
                AssertCapture(capture);
                TestContext.WriteLine(
                    $"DIRECT_CAPTURE path={capture.AbsolutePath} bytes={capture.PngByteCount} " +
                    $"variance={capture.VarianceInvariant} sha256={capture.Sha256}");
            }
            finally
            {
                DestroyProbeMaterials(root);
                Object.Destroy(root);
            }

            yield return null;
            Assert.That(GameObject.Find("VisualEvidenceDirectProbeRoot"), Is.Null);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator CinemachineCaptureProbe_ManualBrainUpdateRendersValidatedPngAndCleansUp()
        {
            RequireGraphicsDevice();
            var outputDirectory = ReadOutputDirectory();
            var root = new GameObject("VisualEvidenceCinemachineProbeRoot");
            try
            {
                var outputCamera = CreateProbeScene(root.transform, "CinemachineOutputCamera");
                var brain = outputCamera.gameObject.AddComponent<CinemachineBrain>();
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                brain.DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.Cut,
                    0f);

                var trackingTarget = new GameObject("CinemachineProbeTrackingTarget");
                trackingTarget.transform.SetParent(root.transform, false);
                trackingTarget.transform.position = Vector3.zero;
                var cameraObject = new GameObject("CinemachineProbeCamera");
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.SetPositionAndRotation(
                    new Vector3(0f, 2.25f, -6f),
                    Quaternion.LookRotation(new Vector3(0f, -0.2f, 1f)));
                var cinemachineCamera = cameraObject.AddComponent<CinemachineCamera>();
                cinemachineCamera.Target = new CameraTarget
                {
                    TrackingTarget = trackingTarget.transform,
                    LookAtTarget = trackingTarget.transform,
                    CustomLookAtTarget = true,
                };

                yield return null;
                brain.ManualUpdate();
                Assert.That(brain.isActiveAndEnabled, Is.True);
                Assert.That(cinemachineCamera.isActiveAndEnabled, Is.True);
                AssertFinite(outputCamera.transform.position);
                AssertFinite(outputCamera.transform.rotation);

                var artifactPath = Path.Combine(
                    outputDirectory,
                    "probe",
                    "cinemachine",
                    "cinemachine-camera.png");
                var capture = VisualEvidenceFrameCapture.CaptureFrame(
                    outputCamera,
                    artifactPath,
                    ProbeWidth,
                    ProbeHeight);
                AssertCapture(capture);
                TestContext.WriteLine(
                    $"CINEMACHINE_CAPTURE path={capture.AbsolutePath} bytes={capture.PngByteCount} " +
                    $"variance={capture.VarianceInvariant} sha256={capture.Sha256} " +
                    $"brain={brain.UpdateMethod}");
            }
            finally
            {
                DestroyProbeMaterials(root);
                Object.Destroy(root);
            }

            yield return null;
            Assert.That(GameObject.Find("VisualEvidenceCinemachineProbeRoot"), Is.Null);
        }

        private static Camera CreateProbeScene(Transform root, string cameraName)
        {
            var cameraObject = new GameObject(cameraName);
            cameraObject.transform.SetParent(root, false);
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 2.25f, -6f),
                Quaternion.LookRotation(new Vector3(0f, -0.2f, 1f)));
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.025f, 0.055f, 1f);
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            CreatePrimitive(
                PrimitiveType.Plane,
                "ProbeGround",
                root,
                new Vector3(0f, -0.75f, 1f),
                new Vector3(0.75f, 1f, 0.75f),
                new Color(0.08f, 0.12f, 0.2f, 1f));
            CreatePrimitive(
                PrimitiveType.Cube,
                "ProbeBoxBlue",
                root,
                new Vector3(-1.1f, 0f, 0.6f),
                new Vector3(1.1f, 1.1f, 1.1f),
                new Color(0.05f, 0.55f, 1f, 1f));
            CreatePrimitive(
                PrimitiveType.Sphere,
                "ProbeSphereOrange",
                root,
                new Vector3(1.1f, 0.15f, 1.2f),
                Vector3.one,
                new Color(1f, 0.32f, 0.06f, 1f));
            return camera;
        }

        private static void CreatePrimitive(
            PrimitiveType primitiveType,
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, true);
            primitive.transform.position = position;
            primitive.transform.localScale = scale;
            var renderer = primitive.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color");
            if (shader == null)
            {
                throw new InvalidOperationException("No unlit shader is available for the capture probe.");
            }

            var material = new Material(shader)
            {
                name = $"{name}_ProbeMaterial",
                color = color,
            };
            renderer.sharedMaterial = material;
        }

        private static void DestroyProbeMaterials(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var material = renderers[index].sharedMaterial;
                renderers[index].sharedMaterial = null;
                if (material != null)
                {
                    Object.Destroy(material);
                }
            }
        }

        private static void AssertCapture(VisualEvidenceCaptureResult capture)
        {
            Assert.That(capture.Width, Is.EqualTo(ProbeWidth));
            Assert.That(capture.Height, Is.EqualTo(ProbeHeight));
            Assert.That(capture.PngByteCount, Is.GreaterThan(VisualEvidenceFrameCapture.MinimumPngByteCount));
            Assert.That(capture.LuminanceVariance, Is.GreaterThan(VisualEvidenceFrameCapture.MinimumLuminanceVariance));
            Assert.That(capture.Sha256, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(File.Exists(capture.AbsolutePath), Is.True);
            VisualEvidenceFrameCapture.ValidatePngFile(
                capture.AbsolutePath,
                ProbeWidth,
                ProbeHeight,
                capture.Sha256);
        }

        private static string ReadOutputDirectory()
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], "-cameraShakeVisualOutput", StringComparison.Ordinal))
                {
                    return Path.GetFullPath(args[index + 1]);
                }
            }

            throw new InvalidOperationException(
                "Missing required -cameraShakeVisualOutput command-line argument.");
        }

        private static void RequireGraphicsDevice()
        {
            Assert.That(
                SystemInfo.graphicsDeviceType,
                Is.Not.EqualTo(GraphicsDeviceType.Null),
                "Capture probe requires UNITY_GRAPHICS=1 and a real graphics device.");
        }

        private static void AssertFinite(Vector3 value)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False);
        }

        private static void AssertFinite(Quaternion value)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False);
            Assert.That(float.IsNaN(value.w) || float.IsInfinity(value.w), Is.False);
        }
    }
}
