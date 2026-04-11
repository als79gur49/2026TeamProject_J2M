using System;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayStageClearOverlay : MonoBehaviour
    {
        private const string ClearLabel = "Clear";

        private GameplaySceneHost _host;
        private GameplayInputHost _inputHost;
        private GUIStyle _labelStyle;
        private GUIStyle _shadowStyle;
        private bool _isVisible;

        public bool IsVisible => _isVisible;

        public void Initialize(GameplaySceneHost host, GameplayInputHost inputHost)
        {
            _host = host;
            BindInputHost(inputHost);
            _isVisible = _host != null && _host.CurrentObjectiveResult.IsCleared;
        }

        private void Update()
        {
            if (!_isVisible &&
                _host != null &&
                _host.CurrentObjectiveResult.IsCleared)
            {
                _isVisible = true;
            }
        }

        private void OnGUI()
        {
            if (!_isVisible)
            {
                return;
            }

            EnsureStyles();

            var width = Screen.width;
            var height = Screen.height;
            var labelHeight = height * 0.2f;
            var rect = new Rect(0f, (height - labelHeight) * 0.5f, width, labelHeight);
            var shadowRect = new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height);

            GUI.Label(shadowRect, ClearLabel, _shadowStyle);
            GUI.Label(rect, ClearLabel, _labelStyle);
        }

        private void OnDestroy()
        {
            BindInputHost(null);
        }

        private void HandleObjectiveUpdated(StageObjectiveTickResult result)
        {
            if (result != null && result.IsCleared)
            {
                _isVisible = true;
            }
        }

        private void BindInputHost(GameplayInputHost inputHost)
        {
            if (_inputHost != null)
            {
                _inputHost.ObjectiveResultUpdated -= HandleObjectiveUpdated;
            }

            _inputHost = inputHost;

            if (_inputHost != null)
            {
                _inputHost.ObjectiveResultUpdated += HandleObjectiveUpdated;
            }
        }

        private void EnsureStyles()
        {
            var fontSize = Mathf.Max(48, Mathf.RoundToInt(Screen.height * 0.11f));

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal =
                    {
                        textColor = Color.white,
                    },
                };
            }

            if (_shadowStyle == null)
            {
                _shadowStyle = new GUIStyle(_labelStyle);
                _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.55f);
            }

            _labelStyle.fontSize = fontSize;
            _shadowStyle.fontSize = fontSize;
        }
    }
}
