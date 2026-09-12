using System;
using System.Linq;
using Game.Exhibition.RestartExperiment;
using UnityEngine;

namespace Game.Exhibition.Integration
{
    public sealed class ResetOverlayTrialOptions
    {
        public ResetOverlayRole Role;
        public string Path, Observation;
        public Exception Error;
        public static bool Present(string[] args) => (args ?? Array.Empty<string>()).Any(a => a != null && a.StartsWith("-j2mResetOverlay", StringComparison.OrdinalIgnoreCase));
        public static ResetOverlayTrialOptions Parse(string[] args, bool enabled)
        {
            if (!Present(args)) return null;
            var result = new ResetOverlayTrialOptions();
            try
            {
                if (!enabled) throw new InvalidOperationException("Reset trial is unsupported by this build.");
                string Value(string key)
                {
                    var indices = Enumerable.Range(0, args.Length).Where(i => args[i] == key).ToArray();
                    if (indices.Length == 0) return null;
                    int i = indices[0];
                    if (indices.Length != 1 || i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal)) throw new ArgumentException("Invalid trial argument: " + key);
                    return args[i + 1];
                }
                foreach (var arg in args.Where(a => a != null))
                {
                    if ((arg.StartsWith("-j2mResetOverlay", StringComparison.OrdinalIgnoreCase) && arg != "-j2mResetOverlayTrial" && arg != "-j2mResetOverlayContext" && arg != "-j2mResetOverlayPhase") ||
                        arg.StartsWith("-j2mOverlayHandoff", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("-j2mSteamSmoke", StringComparison.OrdinalIgnoreCase) ||
                        arg.StartsWith("-j2mSteamAchievementSmoke", StringComparison.OrdinalIgnoreCase) ||
                        (arg.StartsWith("-j2mRestart", StringComparison.OrdinalIgnoreCase) && arg != "-j2mRestartObservation"))
                        throw new ArgumentException("Unknown, mixed or incompatible trial argument.");
                }
                string initial = Value("-j2mResetOverlayTrial"), context = Value("-j2mResetOverlayContext"), phase = Value("-j2mResetOverlayPhase");
                result.Observation = Value("-j2mRestartObservation");
                if (initial != null && context == null && phase == null && result.Observation == null)
                { result.Role = ResetOverlayRole.Initiator; result.Path = initial; }
                else if (initial == null && context != null && phase == "ResetWorker" && result.Observation != null)
                { result.Role = ResetOverlayRole.ResetWorker; result.Path = context; }
                else throw new ArgumentException("Partial or unsupported reset role; FinalObserver is not part of this trial.");
                var providers = args.Where(a => a != null && (a == "-j2mPlatformProvider" || a.StartsWith("-j2mPlatformProvider=", StringComparison.Ordinal))).ToArray();
                if (providers.Length != 1) throw new ArgumentException("Exactly one explicit Steam provider is required.");
                string provider = providers[0].Contains("=") ? providers[0].Substring(providers[0].IndexOf('=') + 1) : Value("-j2mPlatformProvider");
                if (!string.Equals(provider?.Trim(), "steam", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Steam provider required.");
            }
            catch (Exception e) { result.Error = e; }
            return result;
        }
    }

    public sealed class ResetOverlayTrialPresentation : MonoBehaviour
    {
        private ResetOverlayTrial flow;
        private Vector2 scroll;
        private GUISkin panelSkin;
        private int visibility = -1;
        private string keyboardFocus;
        private bool revealFocus;
        private int[] judgments = Array.Empty<int>();
        private static readonly string[] Values = { "earned", "unearned", "inconclusive" };
        private static readonly string[] VisibilityValues = { "opened", "not-visible", "inconclusive" };
        public static ResetOverlayTrialPresentation Attach(ResetOverlayTrial value, string evidenceDirectory)
        {
            var host = new GameObject("Reset overlay trial");
            if (Application.isPlaying) DontDestroyOnLoad(host);
            var view = host.AddComponent<ResetOverlayTrialPresentation>();
            view.flow = value;
            return view;
        }
        private void Start() { if (flow != null) _ = flow.PrepareMenuAsync(); }
        private void Update() => flow?.Tick();
        private void OnEnable() => Application.wantsToQuit += WantsToQuit;
        private void OnDisable() => Application.wantsToQuit -= WantsToQuit;
        private bool WantsToQuit()
        {
            if (flow == null || !flow.HasRuntime || flow.AllowApplicationQuit) return true;
            _ = flow.CloseAsync(); return false;
        }
        private void OnApplicationQuit() => flow?.ObserveExternalQuit();
        private void OnDestroy() { if (panelSkin != null) Destroy(panelSkin); }
        private void Exit() { if (!flow.HasRuntime) Application.Quit(); else _ = flow.CloseAsync(); }
        // The same routing is exercised by the fake panel tests. Shift+Tab remains Steam's shortcut.
        public bool HandleKey(KeyCode key, bool shift = false)
        {
            if (flow == null || (shift && key == KeyCode.Tab)) return false;
            var controls = new System.Collections.Generic.List<string>();
            if (flow.CanReport)
            {
                EnsureJudgments(); controls.Add("visibility");
                if (visibility == 0) for (int i = 0; i < judgments.Length; i++) controls.Add("target-" + i);
                if (ReportComplete) controls.Add("report");
            }
            if (flow.Role == ResetOverlayRole.Initiator && flow.Stage == ResetOverlayStage.Initial && flow.ReportSaved)
            { controls.Add("scope"); if (flow.CanRequest) controls.Add("reset"); }
            if (!flow.ExitRequested) controls.Add("exit");
            if (controls.Count == 0) return false;
            if (key == KeyCode.Tab)
            { keyboardFocus = controls[(controls.IndexOf(keyboardFocus) + 1) % controls.Count]; revealFocus = true; return true; }
            if (!controls.Contains(keyboardFocus)) return false;
            bool activate = key == KeyCode.Return || key == KeyCode.KeypadEnter || key == KeyCode.Space;
            int direction = key == KeyCode.UpArrow || key == KeyCode.LeftArrow ? -1 : key == KeyCode.DownArrow || key == KeyCode.RightArrow || activate ? 1 : 0;
            if (direction == 0) return false;
            if (keyboardFocus == "visibility")
            { visibility = (visibility + direction + 3) % 3; if (visibility > 0) for (int i = 0; i < judgments.Length; i++) judgments[i] = 2; }
            else if (keyboardFocus.StartsWith("target-", StringComparison.Ordinal))
            { int i = int.Parse(keyboardFocus.Substring(7)); judgments[i] = (judgments[i] + direction + 3) % 3; }
            else if (!activate) return false;
            else if (keyboardFocus == "report") SaveReport();
            else if (keyboardFocus == "scope") flow.ScopeConfirmed = !flow.ScopeConfirmed;
            else if (keyboardFocus == "reset") _ = flow.RequestResetAsync();
            else if (keyboardFocus == "exit") Exit();
            return true;
        }
        private bool ReportComplete => visibility >= 0 && judgments.All(j => j >= 0);
        private void EnsureJudgments() { if (judgments.Length != flow.Targets.Length) judgments = Enumerable.Repeat(-1, flow.Targets.Length).ToArray(); }
        private void SaveReport() { if (flow.CanReport && ReportComplete) _ = flow.ReportAsync(VisibilityValues[visibility], judgments.Select(j => Values[j]).ToArray()); }
        private void TrackFocus(string name)
        {
            if (keyboardFocus != name) return;
            GUI.FocusControl(name);
            if (revealFocus && Event.current.type == EventType.Repaint)
            { scroll.y = Math.Max(0, GUILayoutUtility.GetLastRect().y - 45); revealFocus = false; }
        }
        private void OnGUI()
        {
            if (flow == null) return;
            if (Event.current.type == EventType.KeyDown && !Event.current.control && !Event.current.alt && HandleKey(Event.current.keyCode, Event.current.shift)) Event.current.Use();
            var prior = GUI.skin;
            if (panelSkin == null)
            {
                panelSkin = Instantiate(prior);
                panelSkin.label.fontSize = panelSkin.button.fontSize = panelSkin.toggle.fontSize = 18;
                panelSkin.label.wordWrap = panelSkin.button.wordWrap = true;
                panelSkin.button.padding = new RectOffset(8, 8, 7, 7);
            }
            try { GUI.skin = panelSkin; Draw(); }
            finally { GUI.enabled = true; GUI.skin = prior; }
        }
        private void Draw()
        {
            GUI.depth = -1000;
            GUILayout.BeginArea(new Rect(8, 8, Math.Max(100, Math.Min(Screen.width - 16, 820)), Math.Max(80, Screen.height - 16)), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("ACHIEVEMENT DISPLAY TRIAL / " + flow.Role + " / " + flow.Stage);
            GUILayout.Label("초기화 범위: VQ_LEVEL_0_CLEAR ~ VQ_LEVEL_4_CLEAR 업적 5개와 기존 참가자 campaign·로컬 업적 데이터.");
            if (!flow.ReportSaved && flow.Role == ResetOverlayRole.Initiator) GUILayout.Label(flow.BaselineSummary ?? "기준 상태 준비 중");
            if (flow.CanReport)
            {
                GUILayout.Label("Tab: 항목 이동 · 방향키: 선택 · Enter: 실행. Shift+Tab으로 Overlay 업적 목록을 확인한 뒤 게임으로 돌아와 보고하세요. 대상을 식별하지 못하면 판정 불가를 선택하세요.");
                GUI.SetNextControlName("visibility");
                visibility = GUILayout.SelectionGrid(visibility, new[] { "열어 봤고 보임", "열어 봤지만 안 보임", "확인하지 못함" }, 1); TrackFocus("visibility");
                var targets = flow.Targets;
                EnsureJudgments();
                for (int i = 0; i < targets.Length; i++)
                {
                    GUILayout.Label(targets[i] + " / 해당 level clear 업적");
                    if (visibility > 0) judgments[i] = 2;
                    GUI.enabled = visibility == 0;
                    GUI.SetNextControlName("target-" + i);
                    judgments[i] = GUILayout.SelectionGrid(judgments[i], new[] { "획득으로 보임", "미획득으로 보임", "대상/표시 판정 불가" }, 1); TrackFocus("target-" + i);
                    GUI.enabled = true;
                }
                GUI.enabled = ReportComplete;
                GUI.SetNextControlName("report");
                if (GUILayout.Button("대상별 화면 보고 저장")) SaveReport();
                TrackFocus("report");
                GUI.enabled = true;
            }
            if (flow.ReportSaved) GUILayout.Label("관찰 결과 저장 완료");
            if (flow.Role == ResetOverlayRole.Initiator && flow.Stage == ResetOverlayStage.Initial && flow.ReportSaved)
            {
                GUI.SetNextControlName("scope");
                flow.ScopeConfirmed = GUILayout.Toggle(flow.ScopeConfirmed, "위 업적 5개와 참가자 데이터 초기화 범위를 확인했습니다."); TrackFocus("scope");
                GUI.enabled = flow.CanRequest;
                GUI.SetNextControlName("reset");
                if (GUILayout.Button("확인한 범위 초기화 및 게임만 교체 (한 번)")) _ = flow.RequestResetAsync();
                TrackFocus("reset");
                GUI.enabled = true;
            }
            if (flow.IsBusy) GUILayout.Label(flow.ExitRequested ? "종료 예약 — 진행 중인 처리·필수 기록을 마친 뒤 종료합니다." : "처리 중 — 추가 입력 없이 기다려 주세요.");
            if (flow.Stage == ResetOverlayStage.HandoffAccepted) GUILayout.Label("helper가 요청을 수락했습니다. 원본 종료는 위임된 교체를 진행합니다.");
            if (flow.Error != null) GUILayout.Label("시험 중단: " + flow.Error + "\n재시도나 일반 실행으로 복구하지 마세요.");
            GUI.enabled = !flow.ExitRequested;
            GUI.SetNextControlName("exit");
            if (GUILayout.Button("시험 종료 (게임 닫기)")) Exit();
            TrackFocus("exit");
            GUI.enabled = true;
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
    }
}
