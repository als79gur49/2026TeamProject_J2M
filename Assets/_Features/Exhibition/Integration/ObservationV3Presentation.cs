using System;
using System.Linq;
using Game.Exhibition.RestartExperiment;
using UnityEngine;

namespace Game.Exhibition.Integration
{
    /// <summary>Only user reports and flow commands. No SDK getters, capture, or lifecycle ownership.</summary>
    public sealed class ObservationV3Presentation : MonoBehaviour
    {
        private ObservationV3Flow flow;
        private Vector2 scroll;
        private string text = "";
        private string[] choices;
        private readonly string[] targets = ResetOverlayWire.Names();
        public static void Attach(ObservationV3Flow flow)
        {
            var go = new GameObject("Overlay v3 observation panel"); DontDestroyOnLoad(go);
            go.AddComponent<ObservationV3Presentation>().flow = flow;
        }
        private void OnGUI()
        {
            if (flow == null) return;
            GUI.depth = -1000;
            GUILayout.BeginArea(new Rect(8, 8, Math.Max(100, Math.Min(Screen.width - 16, 820)), Math.Max(80, Screen.height - 16)), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Overlay 관찰 / " + flow.Role);
            GUILayout.Label(flow.Status);
            GUILayout.Label("Shift+Tab으로 확인한 내용을 원문으로 입력해 주세요.");
            text = GUILayout.TextArea(text, GUILayout.MinHeight(55));
            GUI.enabled = flow.CanReport && !string.IsNullOrWhiteSpace(text);
            if (GUILayout.Button("Overlay를 열었고 보임")) _ = flow.ReportVisibility("opened", text, null);
            if (GUILayout.Button("열어 봤지만 안 보임")) _ = flow.ReportVisibility("not-visible", text, null);
            if (GUILayout.Button("확인하지 못함")) _ = flow.ReportVisibility("inconclusive", text, null);
            GUI.enabled = flow.CanDisplay && !string.IsNullOrWhiteSpace(text);
            // Fixed rows and an always-present error label keep Layout/Repaint identical.
            if (choices == null || choices.Length != targets.Length) choices = Enumerable.Repeat("inconclusive", targets.Length).ToArray();
            for (int i = 0; i < targets.Length; i++)
            {
                GUI.enabled = flow.CanDisplay && !string.IsNullOrWhiteSpace(text) && flow.Targets.Contains(targets[i]);
                GUILayout.Label(targets[i]);
                int selected = Array.IndexOf(new[] { "earned", "unearned", "inconclusive" }, choices[i]);
                selected = GUILayout.SelectionGrid(selected, new[] { "획득 표시", "미획득 표시", "판정 불가" }, 3);
                choices[i] = new[] { "earned", "unearned", "inconclusive" }[selected];
            }
            GUI.enabled = flow.CanDisplay && !string.IsNullOrWhiteSpace(text);
            if (GUILayout.Button("대상별 표시 보고 저장"))
                _ = flow.ReportDisplay(targets.Select((id, i) => new ObservationV3TargetDisplay { Id = id, Display = choices[i] }).Where(v => flow.Targets.Contains(v.Id)).ToArray(), text, null);
            GUI.enabled = flow.CanFullCycle;
            if (flow.Role == OverlayObservationRole.OriginObserver && GUILayout.Button("Steam 재시작 후 게임 시작 (단회)")) _ = flow.FullCycleAsync();
            GUI.enabled = true;
            GUILayout.Label(flow.Error ?? string.Empty);
            if (GUILayout.Button(flow.CloseLabel)) _ = flow.CloseAsync();
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
    }
}
