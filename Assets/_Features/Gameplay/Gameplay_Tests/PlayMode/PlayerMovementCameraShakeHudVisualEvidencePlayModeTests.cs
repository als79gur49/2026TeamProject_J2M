#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests.Support.Unity;
using Game.Feature.UI.HUD;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed partial class PlayerMovementPlayModeTests
    {
        private const string CameraShakeHudPrefabPath =
            "Assets/_Features/UI/UI_HUD/Prefabs/GameplayHudRoot.prefab";

        [UnityTest]
        [Category("Full")]
        public IEnumerator CameraShakeHudVisualEvidence_FollowThroughFullKeepsOverlayPixelStable()
        {
            var outputRoot = Path.GetFullPath(
                ReadCameraShakeVisualArgument("-cameraShakeVisualOutput"));
            var hudOutput = Path.Combine(outputRoot, "hud");
            Directory.CreateDirectory(hudOutput);

            var profile = LoadCampaignCameraShakeProfile();
            var records = new List<CameraShakeHudFrameRecord>();
            yield return WaitForCameraShakeHudResolution();

            var capture = CaptureFollowThroughCameraShakeHudScenario(profile, hudOutput, records);
            while (capture.MoveNext())
            {
                yield return capture.Current;
            }

            Assert.That(records, Has.Count.EqualTo(3));
            AssertCameraShakeHudScenarioStable(records, "FollowThrough");
            WriteCameraShakeHudManifest(hudOutput, records);
        }

        private static IEnumerator CaptureFollowThroughCameraShakeHudScenario(
            GameplayCameraShakeProfile profile,
            string hudOutput,
            ICollection<CameraShakeHudFrameRecord> records)
        {
            const string scenario = "FollowThrough";
            var cameraObject = new GameObject("CameraShakeHud_FollowThrough_Camera");
            var camera = cameraObject.AddComponent<Camera>();
            var host = CreateHostileFlipCameraShakeHost(
                FlipFloorImpactPresentationKind.FollowThrough,
                camera);
            var materials = new List<Material>();
            CameraShakeHudOverlay overlay = default;
            try
            {
                AddCameraShakeVisualScene(host, materials);
                AddFlipHostileCameraShakeVisualEntities(host, materials);
                overlay = CreateCameraShakeHudOverlay(scenario, camera);
                host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
                host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Full);
                yield return new WaitForSecondsRealtime(2f);
                for (var settleFrame = 0; settleFrame < 8; settleFrame++)
                {
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                }

                host.InputHost.SetRawMoveInput(Vector2.left);
                host.InputHost.BufferFlip();
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                host.InputHost.SetRawMoveInput(Vector2.zero);
                var execute = host.InputHost.RunSingleTick();
                Assert.That(execute, Is.Not.Null);
                var signal = execute.PresentationData.FlipFloorImpactSignals.Single();
                Assert.That(signal.Kind, Is.EqualTo(FlipFloorImpactPresentationKind.FollowThrough));
                double presentationTime = 0d;
                AdvanceFlipHostileVisualTo(
                    host,
                    signal.SourceActionPlanId,
                    MotionTrackProgressSourceKind.LocalMotion,
                    0.85f,
                    ref presentationTime);
                Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.Zero);
                records.Add(CaptureCameraShakeHudFrame(
                    hudOutput,
                    "followthrough-full",
                    "00-pre-landing",
                    scenario,
                    "PreLanding",
                    host,
                    overlay));

                AdvanceFlipHostileVisualTo(
                    host,
                    signal.SourceActionPlanId,
                    MotionTrackProgressSourceKind.LocalMotion,
                    GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime,
                    ref presentationTime);
                Assert.That(host.Presenter.AcceptedGameplayCameraImpulseCount, Is.EqualTo(1));
                host.Presenter.UpdatePresentation(0.02f);
                yield return null;
                records.Add(CaptureCameraShakeHudFrame(
                    hudOutput,
                    "followthrough-full",
                    "03-peak",
                    scenario,
                    "Peak",
                    host,
                    overlay));

                host.Presenter.UpdatePresentation(0.5f);
                yield return null;
                records.Add(CaptureCameraShakeHudFrame(
                    hudOutput,
                    "followthrough-full",
                    "04-rest",
                    scenario,
                    "Rest",
                    host,
                    overlay));
            }
            finally
            {
                DestroyCameraShakeVisualMaterials(materials);
                if (overlay.CanvasObject != null)
                {
                    Object.Destroy(overlay.CanvasObject);
                }

                Object.Destroy(host.gameObject);
                Object.Destroy(cameraObject);
            }

            yield return null;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator CameraShakeHudVisualEvidence_PushFlipFullKeepOverlayPixelStable()
        {
            var outputRoot = Path.GetFullPath(
                ReadCameraShakeVisualArgument("-cameraShakeVisualOutput"));
            var hudOutput = Path.Combine(outputRoot, "hud");
            Directory.CreateDirectory(hudOutput);

            var profile = LoadCampaignCameraShakeProfile();
            var records = new List<CameraShakeHudFrameRecord>();
            yield return WaitForCameraShakeHudResolution();

            var push = CaptureCameraShakeHudScenario(
                "Push",
                profile,
                hudOutput,
                records);
            while (push.MoveNext())
            {
                yield return push.Current;
            }

            var flip = CaptureCameraShakeHudScenario(
                "Flip",
                profile,
                hudOutput,
                records);
            while (flip.MoveNext())
            {
                yield return flip.Current;
            }

            Assert.That(records, Has.Count.EqualTo(6));
            AssertCameraShakeHudScenarioStable(records, "Push");
            AssertCameraShakeHudScenarioStable(records, "Flip");
            WriteCameraShakeHudManifest(hudOutput, records);
        }

        private static IEnumerator CaptureCameraShakeHudScenario(
            string scenario,
            GameplayCameraShakeProfile profile,
            string hudOutput,
            ICollection<CameraShakeHudFrameRecord> records)
        {
            var cameraObject = new GameObject($"CameraShakeHud_{scenario}_Camera");
            var camera = cameraObject.AddComponent<Camera>();
            var host = string.Equals(scenario, "Push", StringComparison.Ordinal)
                ? CreatePushCameraShakeHost(camera)
                : CreateFlipCameraShakeHost(camera);
            var materials = new List<Material>();
            CameraShakeHudOverlay overlay = default;
            try
            {
                AddCameraShakeVisualScene(host, materials);
                overlay = CreateCameraShakeHudOverlay(scenario, camera);
                host.Presenter.ConfigureGameplayCameraShakeProfile(profile);
                host.Presenter.SetCameraMotionLevel(CameraMotionLevel.Full);
                yield return new WaitForSecondsRealtime(2f);
                for (var settleFrame = 0; settleFrame < 8; settleFrame++)
                {
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                }

                if (string.Equals(scenario, "Push", StringComparison.Ordinal))
                {
                    host.InputHost.SetRawMoveInput(Vector2.right);
                    host.InputHost.BufferPush();
                    Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                    var pre = CaptureCameraShakeHudFrame(
                        hudOutput,
                        "push-full",
                        "00-pre-launch",
                        "Push",
                        "PreLaunch",
                        host,
                        overlay);
                    records.Add(pre);

                    var execute = host.InputHost.RunSingleTick();
                    Assert.That(execute, Is.Not.Null);
                    Assert.That(execute.PresentationData.BoxSlideStartSignals, Has.Count.EqualTo(1));
                    host.Presenter.UpdatePresentation(0.02f);
                    yield return null;
                    records.Add(CaptureCameraShakeHudFrame(
                        hudOutput,
                        "push-full",
                        "02-peak",
                        "Push",
                        "Peak",
                        host,
                        overlay));

                    host.Presenter.UpdatePresentation(0.105f);
                    yield return null;
                    records.Add(CaptureCameraShakeHudFrame(
                        hudOutput,
                        "push-full",
                        "04-rest",
                        "Push",
                        "Rest",
                        host,
                        overlay));
                }
                else
                {
                    host.InputHost.SetRawMoveInput(Vector2.left);
                    host.InputHost.BufferFlip();
                    Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                    host.InputHost.SetRawMoveInput(Vector2.zero);
                    var execute = host.InputHost.RunSingleTick();
                    Assert.That(execute, Is.Not.Null);
                    Assert.That(execute.PresentationData.FlipFloorImpactSignals, Has.Count.EqualTo(1));
                    records.Add(CaptureCameraShakeHudFrame(
                        hudOutput,
                        "flip-full",
                        "00-pre-contact",
                        "Flip",
                        "PreContact",
                        host,
                        overlay));

                    var contactNormalized =
                        GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime;
                    for (var step = 0;
                         step < 100 &&
                         ReadCameraShakeVisualProgress(host, TickEntityMotionKind.Flip) < contactNormalized;
                         step++)
                    {
                        host.Presenter.UpdatePresentation(0.005f);
                    }

                    host.Presenter.UpdatePresentation(0.02f);
                    yield return null;
                    records.Add(CaptureCameraShakeHudFrame(
                        hudOutput,
                        "flip-full",
                        "02-peak",
                        "Flip",
                        "Peak",
                        host,
                        overlay));

                    host.Presenter.UpdatePresentation(0.155f);
                    yield return null;
                    records.Add(CaptureCameraShakeHudFrame(
                        hudOutput,
                        "flip-full",
                        "04-rest",
                        "Flip",
                        "Rest",
                        host,
                        overlay));
                }
            }
            finally
            {
                DestroyCameraShakeVisualMaterials(materials);
                if (overlay.CanvasObject != null)
                {
                    Object.Destroy(overlay.CanvasObject);
                }

                Object.Destroy(host.gameObject);
                Object.Destroy(cameraObject);
            }

            yield return null;
        }

        private static CameraShakeHudOverlay CreateCameraShakeHudOverlay(
            string scenario,
            Camera outputCamera)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraShakeHudPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing production HUD prefab: {CameraShakeHudPrefabPath}");

            var canvasObject = new GameObject(
                $"CameraShakeHud_{scenario}_Overlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CameraShakeVisualWidth, CameraShakeVisualHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var root = Object.Instantiate(prefab, canvasObject.transform, false);
            root.name = $"CameraShakeHud_{scenario}_ProductionRoot";
            var hud = root.GetComponent<HUDRootView>();
            Assert.That(hud, Is.Not.Null);
            hud.ValidateAuthoredStructureOrThrow();

            var rootViewModel = new HUDRootViewModel();
            rootViewModel.SetShellState(
                isDimmed: false,
                isGameplayReadOnly: false,
                isPauseButtonEnabled: true);
            hud.Bind(rootViewModel);

            var stageViewModel = new StageInfoViewModel();
            stageViewModel.SetStageName($"{(scenario == "FollowThrough" ? "M2-B" : "M2-A")} {scenario.ToUpperInvariant()} HUD");
            hud.BindStageInfo(stageViewModel);

            var objectiveViewModel = new ObjectiveHudViewModel();
            objectiveViewModel.SetState(
                isVisible: true,
                objectiveStableId: "camera-shake-hud-stability",
                headerText: "CAMERA SHAKE QA",
                rows: new[]
                {
                    new ObjectiveConditionHudViewModel(
                        "overlay-notification",
                        "HUD notification remains screen-fixed",
                        isSatisfied: false,
                        justSatisfied: false),
                });
            hud.ObjectiveHudView.Bind(objectiveViewModel);

            Canvas.ForceUpdateCanvases();
            var pause = root.GetComponentsInChildren<RectTransform>(true)
                .Single(transform => transform.name == "PauseButton");
            var notification = hud.ObjectiveHudView.HeaderLabel.rectTransform;
            return new CameraShakeHudOverlay(
                canvasObject,
                canvas,
                outputCamera,
                root.GetComponent<RectTransform>(),
                notification,
                pause);
        }

        private static CameraShakeHudFrameRecord CaptureCameraShakeHudFrame(
            string hudOutput,
            string scenarioDirectory,
            string frameStem,
            string scenario,
            string marker,
            GameplaySceneHost host,
            CameraShakeHudOverlay overlay)
        {
            Canvas.ForceUpdateCanvases();
            var camera = overlay.OutputCamera;
            var canvas = overlay.Canvas;
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            var target = new RenderTexture(
                CameraShakeVisualWidth,
                CameraShakeVisualHeight,
                24,
                RenderTextureFormat.ARGB32);
            var texture = new Texture2D(
                CameraShakeVisualWidth,
                CameraShakeVisualHeight,
                TextureFormat.RGBA32,
                false);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var previousRenderMode = canvas.renderMode;
            var previousWorldCamera = canvas.worldCamera;
            var previousPlaneDistance = canvas.planeDistance;
            try
            {
                Assert.That(target.Create(), Is.True);
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 1f);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(
                    new Rect(0f, 0f, CameraShakeVisualWidth, CameraShakeVisualHeight),
                    0,
                    0);
                texture.Apply();
                var directory = Path.Combine(hudOutput, scenarioDirectory);
                Directory.CreateDirectory(directory);
                var relativePath = $"{scenarioDirectory}/{frameStem}.png";
                var bytes = texture.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(hudOutput, relativePath), bytes);

                var rig = host.GetComponent<GameplayCameraRig>();
                return new CameraShakeHudFrameRecord
                {
                    Scenario = scenario,
                    Marker = marker,
                    PngPath = relativePath,
                    Sha256 = ComputeCameraShakeHudSha256(bytes),
                    PngBytes = bytes.LongLength,
                    Width = texture.width,
                    Height = texture.height,
                    HudRootScreenPosition = ReadCameraShakeHudScreenPosition(overlay.HudRoot),
                    NotificationScreenPosition = ReadCameraShakeHudScreenPosition(overlay.Notification),
                    PauseScreenPosition = ReadCameraShakeHudScreenPosition(overlay.Pause),
                    AdditivePositionMagnitude = rig.AdditiveLocalPosition.magnitude,
                    AdditiveRotationDegrees = MeasureSmallQuaternionAngleDegrees(
                        rig.AdditiveLocalRotation),
                };
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = previousRenderMode;
                canvas.worldCamera = previousWorldCamera;
                canvas.planeDistance = previousPlaneDistance;
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = previousActive;
                target.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            }
        }

        private static IEnumerator WaitForCameraShakeHudResolution()
        {
            Screen.SetResolution(
                CameraShakeVisualWidth,
                CameraShakeVisualHeight,
                FullScreenMode.Windowed);
            for (var frame = 0;
                 frame < 120 &&
                 (Screen.width != CameraShakeVisualWidth ||
                  Screen.height != CameraShakeVisualHeight);
                 frame++)
            {
                yield return null;
            }

            Assert.That(Screen.width, Is.GreaterThanOrEqualTo(320));
            Assert.That(Screen.height, Is.GreaterThanOrEqualTo(180));
        }

        private static Vector2 ReadCameraShakeHudScreenPosition(RectTransform transform)
        {
            return RectTransformUtility.WorldToScreenPoint(
                null,
                transform.TransformPoint(transform.rect.center));
        }

        private static void AssertCameraShakeHudScenarioStable(
            IEnumerable<CameraShakeHudFrameRecord> records,
            string scenario)
        {
            var selected = records.Where(record => record.Scenario == scenario).ToArray();
            Assert.That(selected, Has.Length.EqualTo(3));
            var pre = selected.Single(record => record.Marker.StartsWith("Pre", StringComparison.Ordinal));
            var peak = selected.Single(record => record.Marker == "Peak");
            var rest = selected.Single(record => record.Marker == "Rest");
            Assert.That(peak.AdditivePositionMagnitude, Is.GreaterThan(0.000001f));
            Assert.That(rest.AdditivePositionMagnitude, Is.EqualTo(0f).Within(0.0000001f));
            AssertCameraShakeHudPositionStable(pre.HudRootScreenPosition, peak.HudRootScreenPosition, scenario, "HUD root");
            AssertCameraShakeHudPositionStable(pre.NotificationScreenPosition, peak.NotificationScreenPosition, scenario, "notification");
            AssertCameraShakeHudPositionStable(pre.PauseScreenPosition, peak.PauseScreenPosition, scenario, "pause control");
            AssertCameraShakeHudPositionStable(pre.HudRootScreenPosition, rest.HudRootScreenPosition, scenario, "HUD root rest");
            Assert.That(peak.Sha256, Is.Not.EqualTo(pre.Sha256), "World shake must change the full-frame pixels.");
        }

        private static void AssertCameraShakeHudPositionStable(
            Vector2 expected,
            Vector2 actual,
            string scenario,
            string element)
        {
            Assert.That(
                Vector2.Distance(actual, expected),
                Is.LessThan(0.01f),
                $"{scenario} {element} moved in screen space during camera shake.");
        }

        private static void WriteCameraShakeHudManifest(
            string hudOutput,
            IEnumerable<CameraShakeHudFrameRecord> records)
        {
            var revisionJson = JsonUtility.ToJson(
                ReadCameraShakeVisualRevisionMetadata(),
                prettyPrint: true).TrimEnd();
            var builder = new StringBuilder();
            builder.Append(revisionJson, 0, revisionJson.Length - 1);
            builder.AppendLine(",");
            builder.AppendLine("  \"captureMethod\": \"Camera.Render RenderTexture with production HUD evidence composition\",");
            builder.AppendLine("  \"runtimeCanvasContract\": \"ScreenSpaceOverlay before and after every capture\",");
            builder.AppendLine($"  \"hudPrefab\": \"{CameraShakeHudPrefabPath}\",");
            builder.AppendLine("  \"frames\": [");
            var items = records.ToArray();
            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                builder.AppendLine("    {");
                builder.AppendLine($"      \"scenario\": \"{item.Scenario}\",");
                builder.AppendLine($"      \"marker\": \"{item.Marker}\",");
                builder.AppendLine($"      \"pngPath\": \"{item.PngPath}\",");
                builder.AppendLine($"      \"sha256\": \"{item.Sha256}\",");
                builder.AppendLine($"      \"pngBytes\": {item.PngBytes},");
                builder.AppendLine($"      \"width\": {item.Width},");
                builder.AppendLine($"      \"height\": {item.Height},");
                AppendCameraShakeHudVector(builder, "hudRoot", item.HudRootScreenPosition, comma: true);
                AppendCameraShakeHudVector(builder, "notification", item.NotificationScreenPosition, comma: true);
                AppendCameraShakeHudVector(builder, "pauseControl", item.PauseScreenPosition, comma: true);
                builder.AppendLine(
                    $"      \"additivePositionMagnitude\": {item.AdditivePositionMagnitude.ToString("R", CultureInfo.InvariantCulture)},");
                builder.AppendLine(
                    $"      \"additiveRotationDegrees\": {item.AdditiveRotationDegrees.ToString("R", CultureInfo.InvariantCulture)}");
                builder.Append("    }");
                builder.AppendLine(index + 1 < items.Length ? "," : string.Empty);
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            File.WriteAllText(Path.Combine(hudOutput, "manifest.json"), builder.ToString());
        }

        private static void AppendCameraShakeHudVector(
            StringBuilder builder,
            string name,
            Vector2 value,
            bool comma)
        {
            builder.Append(
                $"      \"{name}\": {{ \"x\": {value.x.ToString("R", CultureInfo.InvariantCulture)}, " +
                $"\"y\": {value.y.ToString("R", CultureInfo.InvariantCulture)} }}");
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static string ComputeCameraShakeHudSha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }

        private readonly struct CameraShakeHudOverlay
        {
            public CameraShakeHudOverlay(
                GameObject canvasObject,
                Canvas canvas,
                Camera outputCamera,
                RectTransform hudRoot,
                RectTransform notification,
                RectTransform pause)
            {
                CanvasObject = canvasObject;
                Canvas = canvas;
                OutputCamera = outputCamera;
                HudRoot = hudRoot;
                Notification = notification;
                Pause = pause;
            }

            public GameObject CanvasObject { get; }

            public Canvas Canvas { get; }

            public Camera OutputCamera { get; }

            public RectTransform HudRoot { get; }

            public RectTransform Notification { get; }

            public RectTransform Pause { get; }
        }

        private sealed class CameraShakeHudFrameRecord
        {
            public string Scenario { get; set; }

            public string Marker { get; set; }

            public string PngPath { get; set; }

            public string Sha256 { get; set; }

            public long PngBytes { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }

            public Vector2 HudRootScreenPosition { get; set; }

            public Vector2 NotificationScreenPosition { get; set; }

            public Vector2 PauseScreenPosition { get; set; }

            public float AdditivePositionMagnitude { get; set; }

            public float AdditiveRotationDegrees { get; set; }
        }
    }
}
#endif
