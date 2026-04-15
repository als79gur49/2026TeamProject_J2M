using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class GameplayUiCanvasRootView : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _hudLayer;
        [SerializeField] private RectTransform _screenLayer;
        [SerializeField] private RectTransform _popupLayer;
        [SerializeField] private HUDRootView _hudView;
        [SerializeField] private GameplayScreenView _gameplayScreenView;
        [SerializeField] private HelpScreenView _helpScreenView;
        [SerializeField] private ObjectiveStatusScreenView _objectiveStatusScreenView;
        [SerializeField] private PausePopupView _pausePopupView;
        [SerializeField] private ObjectiveInfoPopupView _objectiveInfoPopupView;

        public HUDRootView HudView => _hudView;

        public GameplayScreenView GameplayScreenView => _gameplayScreenView;

        public HelpScreenView HelpScreenView => _helpScreenView;

        public ObjectiveStatusScreenView ObjectiveStatusScreenView => _objectiveStatusScreenView;

        public PausePopupView PausePopupView => _pausePopupView;

        public ObjectiveInfoPopupView ObjectiveInfoPopupView => _objectiveInfoPopupView;

        public void EnsureHierarchy()
        {
            EnsureCanvas();
            EnsureEventSystem();

            _hudLayer = _hudLayer != null ? _hudLayer : CreateLayer("HudLayer", transform);
            _screenLayer = _screenLayer != null ? _screenLayer : CreateLayer("ScreenLayer", transform);
            _popupLayer = _popupLayer != null ? _popupLayer : CreateLayer("PopupLayer", transform);

            if (_hudView == null)
            {
                _hudView = CreateHudView(_hudLayer);
            }

            if (_gameplayScreenView == null)
            {
                _gameplayScreenView = CreateGameplayScreenView(_screenLayer);
            }

            if (_helpScreenView == null)
            {
                _helpScreenView = CreateHelpScreenView(_screenLayer);
            }

            if (_objectiveStatusScreenView == null)
            {
                _objectiveStatusScreenView = CreateObjectiveStatusScreenView(_screenLayer);
            }

            if (_pausePopupView == null)
            {
                _pausePopupView = CreatePausePopupView(_popupLayer);
            }

            if (_objectiveInfoPopupView == null)
            {
                _objectiveInfoPopupView = CreateObjectiveInfoPopupView(_popupLayer);
            }
        }

        private void EnsureCanvas()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
            }

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static RectTransform CreateLayer(string name, Transform parent)
        {
            var layerObject = new GameObject(name, typeof(RectTransform));
            layerObject.transform.SetParent(parent, false);
            var rectTransform = layerObject.GetComponent<RectTransform>();
            Stretch(rectTransform);
            return rectTransform;
        }

        private static HUDRootView CreateHudView(Transform parent)
        {
            var shell = CreatePanel("GameplayHud", parent, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(736f, 224f), new Vector2(16f, 16f));
            var canvasGroup = shell.gameObject.AddComponent<CanvasGroup>();
            var view = shell.gameObject.AddComponent<HUDRootView>();
            var title = CreateLabel("Title", shell, new Vector2(16f, -12f), new Vector2(220f, 24f), TextAnchor.MiddleLeft, 18);
            title.text = "Combat HUD";
            var pause = CreateButton("PauseButton", shell, "Pause", new Vector2(644f, -12f), new Vector2(76f, 28f));

            var playerStatusView = CreatePlayerStatusView(shell);
            var actionBarView = CreateActionBarView(shell);
            var notificationView = CreateNotificationView(shell);

            view.Configure(shell.gameObject, canvasGroup, pause, playerStatusView, actionBarView, notificationView);
            view.IsVisible = true;
            return view;
        }

        private static PlayerStatusView CreatePlayerStatusView(Transform parent)
        {
            var panel = CreatePanel("PlayerStatus", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(214f, 166f), new Vector2(12f, -48f));
            var view = panel.gameObject.AddComponent<PlayerStatusView>();
            var title = CreateLabel("Title", panel, new Vector2(12f, -12f), new Vector2(190f, 22f), TextAnchor.MiddleLeft, 16);
            var hp = CreateLabel("Hp", panel, new Vector2(12f, -40f), new Vector2(190f, 18f));
            var facing = CreateLabel("Facing", panel, new Vector2(12f, -62f), new Vector2(190f, 18f));
            var action = CreateLabel("Action", panel, new Vector2(12f, -84f), new Vector2(190f, 18f));
            var topology = CreateLabel("Topology", panel, new Vector2(12f, -106f), new Vector2(190f, 18f));
            var status = CreateLabel("Status", panel, new Vector2(12f, -128f), new Vector2(190f, 18f));
            var damage = CreateLabel("Damage", panel, new Vector2(12f, -150f), new Vector2(190f, 18f));
            view.Configure(panel.gameObject, title, hp, facing, action, topology, status, damage);
            return view;
        }

        private static ActionBarView CreateActionBarView(Transform parent)
        {
            var panel = CreatePanel("ActionBar", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(220f, 166f), new Vector2(246f, -48f));
            var view = panel.gameObject.AddComponent<ActionBarView>();
            var title = CreateLabel("Title", panel, new Vector2(12f, -12f), new Vector2(196f, 22f), TextAnchor.MiddleLeft, 16);
            var primaryLabel = CreateLabel("PrimaryLabel", panel, new Vector2(12f, -42f), new Vector2(110f, 18f));
            var primaryState = CreateLabel("PrimaryState", panel, new Vector2(12f, -64f), new Vector2(110f, 18f));
            var primaryButton = CreateButton("PrimaryButton", panel, "Use", new Vector2(128f, -46f), new Vector2(80f, 28f));
            var secondaryLabel = CreateLabel("SecondaryLabel", panel, new Vector2(12f, -94f), new Vector2(110f, 18f));
            var secondaryState = CreateLabel("SecondaryState", panel, new Vector2(12f, -116f), new Vector2(110f, 18f));
            var secondaryButton = CreateButton("SecondaryButton", panel, "Use", new Vector2(128f, -98f), new Vector2(80f, 28f));
            var outcome = CreateLabel("Outcome", panel, new Vector2(12f, -144f), new Vector2(196f, 18f));
            var feedback = CreateLabel("Feedback", panel, new Vector2(12f, -164f), new Vector2(196f, 18f));
            view.Configure(
                panel.gameObject,
                title,
                primaryLabel,
                primaryState,
                primaryButton,
                secondaryLabel,
                secondaryState,
                secondaryButton,
                outcome,
                feedback);
            return view;
        }

        private static NotificationView CreateNotificationView(Transform parent)
        {
            var panel = CreatePanel("Notifications", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(246f, 166f), new Vector2(478f, -48f));
            var view = panel.gameObject.AddComponent<NotificationView>();
            var title = CreateLabel("Title", panel, new Vector2(12f, -12f), new Vector2(222f, 22f), TextAnchor.MiddleLeft, 16);
            var first = CreateLabel("First", panel, new Vector2(12f, -42f), new Vector2(222f, 34f), TextAnchor.UpperLeft, 13);
            var second = CreateLabel("Second", panel, new Vector2(12f, -84f), new Vector2(222f, 34f), TextAnchor.UpperLeft, 13);
            var third = CreateLabel("Third", panel, new Vector2(12f, -126f), new Vector2(222f, 34f), TextAnchor.UpperLeft, 13);
            view.Configure(panel.gameObject, title, first, second, third);
            return view;
        }

        private static GameplayScreenView CreateGameplayScreenView(Transform parent)
        {
            var panel = CreatePanel("GameplayScreen", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, 118f), new Vector2(16f, -16f));
            var view = panel.gameObject.AddComponent<GameplayScreenView>();
            var title = CreateLabel("Title", panel, new Vector2(12f, -12f), new Vector2(226f, 24f), TextAnchor.MiddleLeft, 18);
            var help = CreateButton("HelpButton", panel, "Help", new Vector2(12f, -76f), new Vector2(98f, 28f));
            var objective = CreateButton("ObjectiveButton", panel, "Objectives", new Vector2(120f, -76f), new Vector2(118f, 28f));
            view.Configure(panel.gameObject, title, help, objective);
            view.IsVisible = false;
            return view;
        }

        private static HelpScreenView CreateHelpScreenView(Transform parent)
        {
            var panel = CreatePanel("HelpScreen", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(360f, 170f), new Vector2(0f, -20f));
            var view = panel.gameObject.AddComponent<HelpScreenView>();
            var title = CreateLabel("Title", panel, new Vector2(16f, -16f), new Vector2(328f, 24f), TextAnchor.MiddleCenter, 18);
            var description = CreateLabel("Description", panel, new Vector2(16f, -54f), new Vector2(328f, 48f), TextAnchor.UpperCenter, 15);
            var back = CreateButton("BackButton", panel, "Back", new Vector2(131f, -126f), new Vector2(98f, 28f));
            view.Configure(panel.gameObject, title, description, back);
            view.IsVisible = false;
            return view;
        }

        private static ObjectiveStatusScreenView CreateObjectiveStatusScreenView(Transform parent)
        {
            var panel = CreatePanel("ObjectiveStatusScreen", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(440f, 250f), new Vector2(0f, -20f));
            var view = panel.gameObject.AddComponent<ObjectiveStatusScreenView>();
            var title = CreateLabel("Title", panel, new Vector2(16f, -16f), new Vector2(408f, 24f), TextAnchor.MiddleCenter, 18);
            var badge = CreateLabel("Badge", panel, new Vector2(16f, -48f), new Vector2(408f, 22f), TextAnchor.MiddleCenter, 16);
            var summary = CreateLabel("Summary", panel, new Vector2(16f, -78f), new Vector2(408f, 52f), TextAnchor.UpperLeft, 15);
            var detail = CreateLabel("Detail", panel, new Vector2(16f, -138f), new Vector2(408f, 22f), TextAnchor.MiddleLeft, 14);
            var secondary = CreateLabel("Secondary", panel, new Vector2(16f, -164f), new Vector2(408f, 36f), TextAnchor.UpperLeft, 14);
            var overview = CreateButton("OverviewButton", panel, "Overview", new Vector2(16f, -216f), new Vector2(92f, 28f));
            var session = CreateButton("SessionButton", panel, "Session", new Vector2(116f, -216f), new Vector2(92f, 28f));
            var info = CreateButton("InfoButton", panel, "Info", new Vector2(216f, -216f), new Vector2(92f, 28f));
            var back = CreateButton("BackButton", panel, "Back", new Vector2(316f, -216f), new Vector2(92f, 28f));
            view.Configure(panel.gameObject, title, badge, summary, detail, secondary, overview, session, info, back);
            view.IsVisible = false;
            return view;
        }

        private static PausePopupView CreatePausePopupView(Transform parent)
        {
            var panel = CreatePanel("PausePopup", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(280f, 160f), Vector2.zero);
            var view = panel.gameObject.AddComponent<PausePopupView>();
            var title = CreateLabel("Title", panel, new Vector2(16f, -20f), new Vector2(248f, 24f), TextAnchor.MiddleCenter, 18);
            var description = CreateLabel("Description", panel, new Vector2(16f, -56f), new Vector2(248f, 40f), TextAnchor.MiddleCenter, 15);
            var resume = CreateButton("ResumeButton", panel, "Resume", new Vector2(91f, -118f), new Vector2(98f, 28f));
            view.Configure(panel.gameObject, title, description, resume);
            view.IsVisible = false;
            return view;
        }

        private static ObjectiveInfoPopupView CreateObjectiveInfoPopupView(Transform parent)
        {
            var panel = CreatePanel("ObjectiveInfoPopup", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(340f, 200f), new Vector2(0f, -20f));
            var view = panel.gameObject.AddComponent<ObjectiveInfoPopupView>();
            var title = CreateLabel("Title", panel, new Vector2(16f, -16f), new Vector2(308f, 24f), TextAnchor.MiddleCenter, 18);
            var body = CreateLabel("Body", panel, new Vector2(16f, -52f), new Vector2(308f, 86f), TextAnchor.UpperLeft, 14);
            var close = CreateButton("CloseButton", panel, "Close", new Vector2(121f, -160f), new Vector2(98f, 28f));
            view.Configure(panel.gameObject, title, body, close);
            view.IsVisible = false;
            return view;
        }

        private static RectTransform CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta,
            Vector2 anchoredPosition)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);

            var rectTransform = panelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            var image = panelObject.GetComponent<Image>();
            image.color = new Color(0.11f, 0.12f, 0.16f, 0.92f);
            return rectTransform;
        }

        private static Text CreateLabel(
            string name,
            RectTransform parent,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            int fontSize = 14)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);

            var rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            var text = labelObject.GetComponent<Text>();
            text.font = LoadDefaultFont();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(
            string name,
            RectTransform parent,
            string label,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.20f, 0.25f, 0.34f, 1f);

            var button = buttonObject.GetComponent<Button>();

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect);

            var text = labelObject.GetComponent<Text>();
            text.font = LoadDefaultFont();
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;

            return button;
        }

        private static Font LoadDefaultFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                   Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
