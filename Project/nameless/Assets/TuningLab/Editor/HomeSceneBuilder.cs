using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>生成启动主页场景，并保证主页与正式关卡在 Build Settings 中可加载。</summary>
static class HomeSceneBuilder
{
    const string HomeScenePath = "Assets/Scenes/Home.unity";
    const string LevelScenePath = "Assets/Scenes/Levels/Final Map.unity";

    [MenuItem("GameJam/Rebuild Home Scene")]
    public static void RebuildHomeScene()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.cullingMask = 0;
        cameraObject.AddComponent<AudioListener>();

        var homeObject = new GameObject("Home");
        homeObject.AddComponent<HomeScreen>();
        homeObject.AddComponent<LabSettingsUI>();

        EditorSceneManager.SaveScene(scene, HomeScenePath);
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
    }

    static void UpdateBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(HomeScenePath, true),
            new EditorBuildSettingsScene(LevelScenePath, true)
        };

        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        for (int i = 0; i < current.Length; i++)
        {
            string path = current[i].path;
            if (path == HomeScenePath || path == LevelScenePath) continue;
            scenes.Add(current[i]);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
