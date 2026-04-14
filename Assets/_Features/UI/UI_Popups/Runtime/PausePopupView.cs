using System;
using UnityEngine;

namespace Game.Feature.UI.Popups
{
    public sealed class PausePopupView : MonoBehaviour
    {
        public event Action ResumeRequested;

        public bool IsVisible { get; set; }

        public void ClickResume()
        {
            if (!IsVisible)
            {
                return;
            }

            ResumeRequested?.Invoke();
        }

        private void OnGUI()
        {
            if (!IsVisible)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 120f, Screen.height * 0.5f - 70f, 240f, 140f), GUI.skin.window);
            GUILayout.Label("Paused");
            GUILayout.Label("Modal popup");
            if (GUILayout.Button("Resume"))
            {
                ClickResume();
            }

            GUILayout.EndArea();
        }
    }
}
