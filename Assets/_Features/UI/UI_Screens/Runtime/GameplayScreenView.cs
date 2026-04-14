using System;
using UnityEngine;

namespace Game.Feature.UI.Screens
{
    public sealed class GameplayScreenView : MonoBehaviour
    {
        public event Action HelpRequested;

        public bool IsVisible { get; set; }

        public void ClickHelp()
        {
            if (!IsVisible)
            {
                return;
            }

            HelpRequested?.Invoke();
        }

        private void OnGUI()
        {
            if (!IsVisible)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(12f, 12f, 200f, 90f), GUI.skin.box);
            GUILayout.Label("Gameplay Screen");
            if (GUILayout.Button("Help"))
            {
                ClickHelp();
            }

            GUILayout.EndArea();
        }
    }
}
