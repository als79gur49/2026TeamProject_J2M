using Game.Feature.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    internal static class GameplayLegacyHudViewFactory
    {
        internal static HUDRootView Create(RectTransform parent)
        {
            var shell = UiCanvasElementFactory.CreatePanel(
                "GameplayHud",
                parent,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(736f, 224f),
                new Vector2(16f, 16f));
            var canvasGroup = shell.gameObject.AddComponent<CanvasGroup>();
            var view = shell.gameObject.AddComponent<HUDRootView>();
            var title = UiCanvasElementFactory.CreateLabel(
                "Title",
                shell,
                new Vector2(16f, -12f),
                new Vector2(220f, 24f),
                TextAnchor.MiddleLeft,
                18);
            title.text = "Combat HUD";
            var pause = UiCanvasElementFactory.CreateButton(
                "PauseButton",
                shell,
                "Pause",
                new Vector2(644f, -12f),
                new Vector2(76f, 28f));

            var playerStatusView = CreatePlayerStatusView(shell);
            var actionBarView = CreateActionBarView(shell);
            var notificationView = CreateNotificationView(shell);

            view.Configure(shell.gameObject, canvasGroup, pause, playerStatusView, actionBarView, notificationView);
            view.IsVisible = true;
            return view;
        }

        private static PlayerStatusView CreatePlayerStatusView(Transform parent)
        {
            var panel = UiCanvasElementFactory.CreatePanel(
                "PlayerStatus",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(214f, 166f),
                new Vector2(12f, -48f));
            var view = panel.gameObject.AddComponent<PlayerStatusView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(12f, -12f), new Vector2(190f, 22f), TextAnchor.MiddleLeft, 16);
            var hp = UiCanvasElementFactory.CreateLabel("Hp", panel, new Vector2(12f, -40f), new Vector2(190f, 18f));
            var facing = UiCanvasElementFactory.CreateLabel("Facing", panel, new Vector2(12f, -62f), new Vector2(190f, 18f));
            var action = UiCanvasElementFactory.CreateLabel("Action", panel, new Vector2(12f, -84f), new Vector2(190f, 18f));
            var topology = UiCanvasElementFactory.CreateLabel("Topology", panel, new Vector2(12f, -106f), new Vector2(190f, 18f));
            var status = UiCanvasElementFactory.CreateLabel("Status", panel, new Vector2(12f, -128f), new Vector2(190f, 18f));
            var damage = UiCanvasElementFactory.CreateLabel("Damage", panel, new Vector2(12f, -150f), new Vector2(190f, 18f));
            view.Configure(panel.gameObject, title, hp, facing, action, topology, status, damage);
            return view;
        }

        private static ActionBarView CreateActionBarView(Transform parent)
        {
            var panel = UiCanvasElementFactory.CreatePanel(
                "ActionBar",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(220f, 166f),
                new Vector2(246f, -48f));
            var view = panel.gameObject.AddComponent<ActionBarView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(12f, -12f), new Vector2(196f, 22f), TextAnchor.MiddleLeft, 16);
            var primaryLabel = UiCanvasElementFactory.CreateLabel("PrimaryLabel", panel, new Vector2(12f, -42f), new Vector2(110f, 18f));
            var primaryState = UiCanvasElementFactory.CreateLabel("PrimaryState", panel, new Vector2(12f, -64f), new Vector2(110f, 18f));
            var primaryButton = UiCanvasElementFactory.CreateButton("PrimaryButton", panel, "Use", new Vector2(128f, -46f), new Vector2(80f, 28f));
            var secondaryLabel = UiCanvasElementFactory.CreateLabel("SecondaryLabel", panel, new Vector2(12f, -94f), new Vector2(110f, 18f));
            var secondaryState = UiCanvasElementFactory.CreateLabel("SecondaryState", panel, new Vector2(12f, -116f), new Vector2(110f, 18f));
            var secondaryButton = UiCanvasElementFactory.CreateButton("SecondaryButton", panel, "Use", new Vector2(128f, -98f), new Vector2(80f, 28f));
            var outcome = UiCanvasElementFactory.CreateLabel("Outcome", panel, new Vector2(12f, -144f), new Vector2(196f, 18f));
            var feedback = UiCanvasElementFactory.CreateLabel("Feedback", panel, new Vector2(12f, -164f), new Vector2(196f, 18f));
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
            var panel = UiCanvasElementFactory.CreatePanel(
                "Notifications",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(246f, 166f),
                new Vector2(478f, -48f));
            var view = panel.gameObject.AddComponent<NotificationView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(12f, -12f), new Vector2(222f, 22f), TextAnchor.MiddleLeft, 16);
            var first = UiCanvasElementFactory.CreateLabel("First", panel, new Vector2(12f, -42f), new Vector2(222f, 34f), TextAnchor.UpperLeft, 13);
            var second = UiCanvasElementFactory.CreateLabel("Second", panel, new Vector2(12f, -84f), new Vector2(222f, 34f), TextAnchor.UpperLeft, 13);
            var third = UiCanvasElementFactory.CreateLabel("Third", panel, new Vector2(12f, -126f), new Vector2(222f, 34f), TextAnchor.UpperLeft, 13);
            view.Configure(panel.gameObject, title, first, second, third);
            return view;
        }
    }
}
