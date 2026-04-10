using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class TopologyTransitionPostFxDebugOverlay : MonoBehaviour
    {
        private const float MaxPanelWidth = 360f;
        private const float ScreenMargin = 20f;
        private const string PanelTitle = "Topology Transition Blur Debug";

        [SerializeField] private bool visible = true;

        private GameplayTickViewPresenter _presenter;
        private TopologyTransitionPostFxController _controller;
        private string _currentDebugText = "Presenter: unavailable";
        private Texture2D _panelBackground;
        private GUIStyle _bodyStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;

        public string CurrentDebugText => _currentDebugText;

        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        public void Initialize(
            GameplayTickViewPresenter presenter,
            TopologyTransitionPostFxController controller)
        {
            _presenter = presenter;
            _controller = controller;
            RefreshSnapshot();
        }

        public void RefreshSnapshot()
        {
            var builder = new StringBuilder(256);
            var visualState = _presenter?.CurrentTopologyTransitionVisualState;
            var outputCamera = _controller?.OutputCamera;
            var cameraData = outputCamera != null
                ? outputCamera.GetUniversalAdditionalCameraData()
                : null;
            var motionBlur = _controller?.MotionBlurOverride;
            var runtimeVolume = _controller?.RuntimeVolume;
            var runtimeProfile = _controller?.RuntimeVolumeProfile;

            AppendLine(builder, "Phase", _presenter?.CurrentPresentationPhase.ToString() ?? "Unavailable");
            AppendLine(builder, "Presentation Active", FormatBool(_presenter?.IsPresentationActive ?? false));
            AppendLine(builder, "Transition Active", FormatBool(visualState?.IsActive ?? false));
            AppendLine(builder, "Progress01", FormatFloat(visualState?.Progress01 ?? 0f));
            AppendLine(
                builder,
                "Angular Velocity",
                FormatFloat(visualState?.AngularVelocityNormalized ?? 0f));
            AppendLine(builder, "Blur Intensity", FormatFloat(motionBlur?.intensity.value ?? 0f));
            AppendLine(builder, "Blur Mode", motionBlur?.mode.value.ToString() ?? "Unavailable");
            AppendLine(builder, "Blur Quality", motionBlur?.quality.value.ToString() ?? "Unavailable");
            AppendLine(builder, "Camera Clamp", FormatFloat(motionBlur?.clamp.value ?? 0f));
            AppendLine(builder, "Output Camera", outputCamera != null ? outputCamera.name : "Unavailable");
            AppendLine(builder, "Post Processing", FormatBool(cameraData?.renderPostProcessing ?? false));
            AppendLine(builder, "Runtime Volume", FormatBool(runtimeVolume != null && runtimeVolume.enabled));
            AppendLine(builder, "Runtime Profile", runtimeProfile != null ? runtimeProfile.name : "Unavailable");

            _currentDebugText = builder.ToString();
        }

        private void LateUpdate()
        {
            RefreshSnapshot();
        }

        private void OnDestroy()
        {
            if (_panelBackground != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_panelBackground);
                }
                else
                {
                    DestroyImmediate(_panelBackground);
                }

                _panelBackground = null;
            }
        }

        private void OnGUI()
        {
            if (!visible || !ShouldDrawInCurrentBuild())
            {
                return;
            }

            EnsureStyles();

            var panelWidth = Mathf.Min(MaxPanelWidth, Mathf.Max(300f, Screen.width - (ScreenMargin * 2f)));
            var contentWidth = panelWidth - 32f;
            var titleHeight = _titleStyle.CalcHeight(new GUIContent(PanelTitle), contentWidth);
            var bodyHeight = _bodyStyle.CalcHeight(new GUIContent(_currentDebugText), contentWidth);
            var panelHeight = Mathf.Max(156f, titleHeight + bodyHeight + 32f);
            var panelX = Mathf.Max(ScreenMargin, Screen.width - panelWidth - ScreenMargin);
            var panelY = Mathf.Max(ScreenMargin, Screen.height - panelHeight - ScreenMargin);

            var panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);
            GUI.Box(panelRect, GUIContent.none, _panelStyle);

            var titleRect = new Rect(panelRect.x + 16f, panelRect.y + 12f, contentWidth, titleHeight);
            GUI.Label(titleRect, PanelTitle, _titleStyle);

            var bodyRect = new Rect(titleRect.x, titleRect.yMax + 8f, contentWidth, bodyHeight);
            GUI.Label(bodyRect, _currentDebugText, _bodyStyle);
        }

        private void EnsureStyles()
        {
            if (_panelStyle == null)
            {
                _panelBackground = CreatePanelBackground();
                _panelStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(14, 14, 14, 14),
                };
                _panelStyle.normal.background = _panelBackground;
                _panelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.98f);
            }

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.96f, 0.97f, 0.99f) },
                    wordWrap = true,
                };
            }

            if (_bodyStyle == null)
            {
                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    normal = { textColor = new Color(0.89f, 0.91f, 0.95f) },
                    wordWrap = true,
                    richText = false,
                };
            }
        }

        private static void AppendLine(StringBuilder builder, string label, string value)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(label).Append(": ").Append(value);
        }

        private static string FormatBool(bool value)
        {
            return value ? "On" : "Off";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.000");
        }

        private static bool ShouldDrawInCurrentBuild()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }

        private static Texture2D CreatePanelBackground()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixel(0, 0, new Color(0.07f, 0.09f, 0.14f, 0.88f));
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }
    }
}
