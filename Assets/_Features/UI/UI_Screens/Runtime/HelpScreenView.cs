using System;
using UnityEngine;

namespace Game.Feature.UI.Screens
{
    public sealed class HelpScreenView : MonoBehaviour
    {
        public event Action BackRequested;

        public bool IsVisible { get; set; }

        public void ClickBack()
        {
            if (!IsVisible)
            {
                return;
            }

            BackRequested?.Invoke();
        }

        private void OnGUI()
        {
            if (!IsVisible)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 160f, 20f, 320f, 140f), GUI.skin.box);
            GUILayout.Label("Help Screen");
            GUILayout.Label("Flow validation screen");
            if (GUILayout.Button("Back"))
            {
                ClickBack();
            }

            GUILayout.EndArea();
        }
    }
}
