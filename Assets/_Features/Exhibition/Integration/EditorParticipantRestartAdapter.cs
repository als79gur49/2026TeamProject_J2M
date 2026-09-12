#if UNITY_EDITOR
using System;
using System.IO;
using Game.Feature.Stages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Exhibition.Integration
{
    [InitializeOnLoad]
    public sealed class EditorParticipantRestartAdapter : IParticipantRestart
    {
        private const string RestartKey = "J2M.ParticipantReset.Restart";
        private const string RestoreKey = "J2M.ParticipantReset.RestoreScene";
        private const string PreviousSceneKey = "J2M.ParticipantReset.PreviousScene";
        private const string MainMenuPath = "Assets/Scenes/MainMenuScene.unity";

        static EditorParticipantRestartAdapter()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public void ValidateAvailable()
        {
            if (!EditorApplication.isPlaying || Application.isBatchMode ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath) == null)
                throw new InvalidOperationException("메인메뉴 Play를 다시 시작할 수 없습니다.");
        }

        public void Restart(ResetIdentity identity)
        {
            ValidateAvailable();
            SessionState.SetBool(RestartKey, true);
            EditorApplication.isPlaying = false;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && !Application.isBatchMode)
            {
                bool pending;
                try
                {
                    var path = Path.Combine(new ApplicationPersistentDataSavePathProvider().SaveRootPath,
                        "exhibition-reset.json");
                    pending = new FileExhibitionResetJournal(path).Load()?.State == ResetRecord.Pending;
                }
                catch { pending = true; }
                if (!pending && !SessionState.GetBool(RestartKey, false)) return;
                if (!SessionState.GetBool(RestoreKey, false))
                {
                    SessionState.SetString(PreviousSceneKey,
                        AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                    SessionState.SetBool(RestoreKey, true);
                }
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
                EditorDirectPlayContextStore.Clear();
                StageLaunchContextStore.Clear();
            }
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(RestartKey, false);
                RestoreStartScene();
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            // Native shutdown and MonoBehaviour OnDestroy have completed before another Play session starts.
            ExhibitionApplication.ReleaseSessionLock();
            RestoreStartScene();
            if (!SessionState.GetBool(RestartKey, false)) return;
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(RestartKey, false) && !EditorApplication.isPlaying)
                    EditorApplication.isPlaying = true;
            };
        }

        private static void RestoreStartScene()
        {
            if (!SessionState.GetBool(RestoreKey, false)) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SessionState.GetString(PreviousSceneKey, string.Empty));
            SessionState.SetBool(RestoreKey, false);
        }
    }
}
#endif
