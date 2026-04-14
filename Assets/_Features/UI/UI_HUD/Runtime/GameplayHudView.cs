using System;
using UnityEngine;

namespace Game.Feature.UI.HUD
{
    public sealed class GameplayHudView : MonoBehaviour
    {
        public event Action MoveUpRequested;

        public event Action FlipRightRequested;

        public event Action PauseRequested;

        public bool IsVisible { get; set; } = true;

        public GameplayHudViewModel ViewModel { get; private set; }

        public void Bind(GameplayHudViewModel viewModel)
        {
            ViewModel = viewModel;
        }

        public void ClickFlipRight()
        {
            if (!IsVisible || ViewModel == null || !ViewModel.IsInteractive)
            {
                return;
            }

            FlipRightRequested?.Invoke();
        }

        public void ClickMoveUp()
        {
            if (!IsVisible || ViewModel == null || !ViewModel.IsInteractive)
            {
                return;
            }

            MoveUpRequested?.Invoke();
        }

        public void ClickPause()
        {
            if (!IsVisible || ViewModel == null || !ViewModel.IsInteractive)
            {
                return;
            }

            PauseRequested?.Invoke();
        }

        private void OnGUI()
        {
            if (!IsVisible || ViewModel == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(12f, Screen.height - 210f, 260f, 198f), GUI.skin.box);
            GUILayout.Label("Gameplay HUD");
            GUILayout.Label($"HP: {ViewModel.CurrentHp}");
            GUILayout.Label($"Facing: {ViewModel.Facing}");
            GUILayout.Label($"Action: {ViewModel.ActiveActionKind}");
            GUILayout.Label($"Topology: {ViewModel.CurrentTopology.BottomFace}");
            GUILayout.Label($"Paused: {ViewModel.IsPaused}");
            GUILayout.Label($"Ready: {ViewModel.CanAcceptGameplayCommands}");

            var previousEnabled = GUI.enabled;
            GUI.enabled = ViewModel.IsInteractive;
            if (GUILayout.Button("Move Up"))
            {
                ClickMoveUp();
            }

            GUI.enabled = ViewModel.IsInteractive;
            if (GUILayout.Button("Flip Right"))
            {
                ClickFlipRight();
            }

            GUI.enabled = ViewModel.IsInteractive;
            if (GUILayout.Button("Pause"))
            {
                ClickPause();
            }

            GUI.enabled = previousEnabled;

            if (!string.IsNullOrEmpty(ViewModel.FeedbackText))
            {
                GUILayout.Label($"Feedback: {ViewModel.FeedbackText}");
            }

            GUILayout.EndArea();
        }
    }
}
