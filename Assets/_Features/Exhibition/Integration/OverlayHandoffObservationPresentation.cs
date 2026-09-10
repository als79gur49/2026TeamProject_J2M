using System;
using UnityEngine;

namespace Game.Exhibition.Integration
{
    /// <summary>Minimal user report panel; visibility is never inferred from runtime data.</summary>
    public sealed class OverlayHandoffObservationPresentation : MonoBehaviour
    {
        private OverlayHandoffObservation flow;
        private Vector2 scroll;
        private GUISkin panelSkin;
        public static OverlayHandoffObservationPresentation Attach(OverlayHandoffObservation flow)
        {
            var host = new GameObject("Overlay handoff observation");
            if (Application.isPlaying) DontDestroyOnLoad(host);
            var view = host.AddComponent<OverlayHandoffObservationPresentation>();
            view.flow = flow;
            return view;
        }
        private void Start()
        {
            // Preserve preparation when menu composition has not called its preparation port.
            if (flow != null) _ = flow.PrepareMenuAsync();
        }
        private void Update() => flow?.Tick();
        private void OnApplicationQuit() => flow?.FinalizeObservation();
        private void OnDestroy() { if (panelSkin != null) Destroy(panelSkin); }

        private void OnGUI()
        {
            if (flow == null) return;
            var previousSkin = GUI.skin;
            if (panelSkin == null)
            {
                panelSkin = Instantiate(previousSkin);
                panelSkin.label.fontSize = panelSkin.button.fontSize = panelSkin.textField.fontSize = 18;
                panelSkin.label.wordWrap = panelSkin.button.wordWrap = true;
                panelSkin.button.padding = new RectOffset(8, 8, 7, 7);
                panelSkin.textField.padding = new RectOffset(6, 6, 7, 7);
            }
            try { GUI.skin = panelSkin; DrawPanel(); }
            finally { GUI.skin = previousSkin; }
        }

        private void DrawPanel()
        {
            GUI.depth = -1000;
            GUILayout.BeginArea(new Rect(8, 8, Math.Max(100, Math.Min(Screen.width - 16, 820)), Math.Max(80, Screen.height - 16)), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("OVERLAY HANDOFF OBSERVATION / " + flow.Role + " / " + flow.Stage);
            GUILayout.Label("업적·참가자 데이터를 초기화하지 않고 Overlay 화면 표시를 비교합니다.");
            if (flow.CanReport)
                GUILayout.Label("Shift+Tab으로 Overlay를 열어 보고, 게임으로 돌아와 결과를 알려 주세요.");
            GUI.enabled = flow.CanReport;
            if (GUILayout.Button("열어 봤고 보임")) _ = flow.ReportAsync(OverlayVisibility.Opened);
            if (GUILayout.Button("열어 봤지만 안 보임")) _ = flow.ReportAsync(OverlayVisibility.NotVisible);
            if (GUILayout.Button("확인하지 못함/판정 불가")) _ = flow.ReportAsync(OverlayVisibility.Inconclusive);
            GUI.enabled = true;
            if (flow.Visibility.HasValue) GUILayout.Label("관찰 결과 저장 완료");
            GUI.enabled = flow.CanReplace;
            if (GUILayout.Button("게임만 교체 (한 번)")) _ = flow.ReplaceAsync();
            GUI.enabled = true;
            if (flow.Stage == OverlayObservationStage.HandoffAccepted)
                GUILayout.Label("helper가 교체 요청을 수락했습니다. 원본 종료는 위임된 교체를 진행시킵니다.");
            if (flow.Error != null) GUILayout.Label("최초 핵심 오류: " + flow.Error);
            if (GUILayout.Button("진단 종료 (게임 닫기)"))
            {
                if (!flow.HasRuntime) Application.Quit();
                else _ = flow.CloseAsync();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
