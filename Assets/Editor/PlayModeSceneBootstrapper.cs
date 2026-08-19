#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlayModeSceneBootstrapper
{
    private const string InitScenePath = "Assets/Scenes/InitScene.unity"; // InitScene 경로

    static PlayModeSceneBootstrapper()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // 플레이 모드 진입 직전, 실행 씬을 InitScene으로 강제 지정
            SceneAsset initScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(InitScenePath);
            if (initScene != null)
            {
                EditorSceneManager.playModeStartScene = initScene;
            }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            // 에디터 모드로 돌아오면 자동 시작 씬 설정을 해제
            EditorSceneManager.playModeStartScene = null;
        }
    }
}
#endif